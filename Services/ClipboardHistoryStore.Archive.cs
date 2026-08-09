using System.IO;
using System.Text.Json;
using ClipboardManager.Models;
using ClipboardManager.Services;
using Microsoft.Data.Sqlite;

namespace ClipboardManager;

internal sealed partial class ClipboardHistoryStore
{
    private static void EnsureArchiveBucketTable(SqliteConnection conn, SqliteTransaction? tx, int bucketNo)
    {
        var table = ArchiveTableName(bucketNo);
        using (var cmd = CreateCommand(conn, tx,
            $"""
             CREATE TABLE IF NOT EXISTS {table} (
               archive_id INTEGER PRIMARY KEY AUTOINCREMENT,
               original_id INTEGER NOT NULL,
               entry_type INTEGER NOT NULL,
               text_content TEXT,
               image_blob BLOB,
               image_w INTEGER NOT NULL DEFAULT 0,
               image_h INTEGER NOT NULL DEFAULT 0,
               file_paths_json TEXT,
               copied_at_ms INTEGER NOT NULL,
               pinyin_blob TEXT,
               archived_at_ms INTEGER NOT NULL
             );
             """))
        {
            cmd.ExecuteNonQuery();
        }

        using (var cmd = CreateCommand(conn, tx,
            $"CREATE INDEX IF NOT EXISTS idx_{table}_copied ON {table}(copied_at_ms DESC);"))
        {
            cmd.ExecuteNonQuery();
        }

        EnsureArchiveBucketSearchIndex(conn, tx, bucketNo);
    }

    private static void EnsureArchiveBucketSearchIndex(SqliteConnection conn, SqliteTransaction? tx, int bucketNo)
    {
        var table = ArchiveTableName(bucketNo);
        var fts = $"{table}_fts";
        bool exists;
        using (var check = CreateCommand(conn, tx,
            "SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = @name LIMIT 1"))
        {
            check.Parameters.AddWithValue("@name", fts);
            exists = check.ExecuteScalar() != null;
        }

        using (var create = CreateCommand(conn, tx,
            $"""
             CREATE VIRTUAL TABLE IF NOT EXISTS {fts} USING fts5(
               text_content,
               file_paths_json,
               pinyin_blob,
               content='{table}',
               content_rowid='archive_id',
               tokenize='trigram'
             );
             CREATE TRIGGER IF NOT EXISTS t_{table}_ai AFTER INSERT ON {table} BEGIN
               INSERT INTO {fts}(rowid, text_content, file_paths_json, pinyin_blob)
               VALUES (new.archive_id, new.text_content, new.file_paths_json, new.pinyin_blob);
             END;
             CREATE TRIGGER IF NOT EXISTS t_{table}_ad AFTER DELETE ON {table} BEGIN
               INSERT INTO {fts}({fts}, rowid, text_content, file_paths_json, pinyin_blob)
               VALUES ('delete', old.archive_id, old.text_content, old.file_paths_json, old.pinyin_blob);
             END;
             CREATE TRIGGER IF NOT EXISTS t_{table}_au AFTER UPDATE ON {table} BEGIN
               INSERT INTO {fts}({fts}, rowid, text_content, file_paths_json, pinyin_blob)
               VALUES ('delete', old.archive_id, old.text_content, old.file_paths_json, old.pinyin_blob);
               INSERT INTO {fts}(rowid, text_content, file_paths_json, pinyin_blob)
               VALUES (new.archive_id, new.text_content, new.file_paths_json, new.pinyin_blob);
             END;
             """))
        {
            create.ExecuteNonQuery();
        }

        if (!exists)
        {
            using var populate = CreateCommand(conn, tx,
                $"""
                 INSERT INTO {fts}(rowid, text_content, file_paths_json, pinyin_blob)
                 SELECT archive_id, text_content, file_paths_json, pinyin_blob FROM {table};
                 """);
            populate.ExecuteNonQuery();
        }
    }

    public void TryDeleteArchived(ClipboardEntry entry)
    {
        if (entry.ArchiveBucketNo is not int bucketNo || entry.ArchiveId is not long archiveId) return;
        try
        {
            using var conn = Open();
            using var tx = conn.BeginTransaction();
            var table = ArchiveTableName(bucketNo);
            using (var delete = CreateCommand(conn, tx, $"DELETE FROM {table} WHERE archive_id = @archiveId"))
            {
                delete.Parameters.AddWithValue("@archiveId", archiveId);
                if (delete.ExecuteNonQuery() == 0) return;
            }

            using (var update = CreateCommand(conn, tx,
                """
                UPDATE clipboard_history_archive_buckets
                SET item_count = MAX(0, item_count - 1)
                WHERE bucket_no = @bucketNo
                """))
            {
                update.Parameters.AddWithValue("@bucketNo", bucketNo);
                update.ExecuteNonQuery();
            }
            tx.Commit();
        }
        catch
        {
            // ignore
        }
    }

    public bool TryRestoreArchived(ClipboardEntry entry)
    {
        if (!entry.IsArchived) return entry.PersistedId.HasValue;
        var bucketNo = entry.ArchiveBucketNo;
        var archiveId = entry.ArchiveId;
        entry.ArchiveBucketNo = null;
        entry.ArchiveId = null;
        if (!TryInsert(entry))
        {
            entry.ArchiveBucketNo = bucketNo;
            entry.ArchiveId = archiveId;
            return false;
        }

        TryDeleteArchived(new ClipboardEntry { ArchiveBucketNo = bucketNo, ArchiveId = archiveId });
        return true;
    }

    private static int GetWritableArchiveBucket(SqliteConnection conn, SqliteTransaction tx)
    {
        using (var find = CreateCommand(conn, tx,
            """
            SELECT bucket_no
            FROM clipboard_history_archive_buckets
            WHERE item_count < @maxItems
            ORDER BY bucket_no DESC
            LIMIT 1
            """))
        {
            find.Parameters.AddWithValue("@maxItems", ArchiveBucketMaxItems);
            var existing = find.ExecuteScalar();
            if (existing != null && existing != DBNull.Value)
            {
                var bucketNo = Convert.ToInt32(existing);
                EnsureArchiveBucketTable(conn, tx, bucketNo);
                return bucketNo;
            }
        }

        int nextBucketNo;
        using (var max = CreateCommand(conn, tx,
            "SELECT COALESCE(MAX(bucket_no), 0) + 1 FROM clipboard_history_archive_buckets"))
        {
            nextBucketNo = Convert.ToInt32(max.ExecuteScalar());
        }

        EnsureArchiveBucketTable(conn, tx, nextBucketNo);
        using (var insert = CreateCommand(conn, tx,
            """
            INSERT INTO clipboard_history_archive_buckets
              (bucket_no, table_name, item_count, created_at_ms)
            VALUES (@bucketNo, @tableName, 0, @createdAtMs)
            """))
        {
            insert.Parameters.AddWithValue("@bucketNo", nextBucketNo);
            insert.Parameters.AddWithValue("@tableName", ArchiveTableName(nextBucketNo));
            insert.Parameters.AddWithValue("@createdAtMs", NowMs());
            insert.ExecuteNonQuery();
        }

        return nextBucketNo;
    }

    private static bool ArchiveAndDeleteCore(SqliteConnection conn, SqliteTransaction tx, long id)
    {
        int entryType;
        object textContent;
        object imageBlob;
        int imageW;
        int imageH;
        object filePathsJson;
        long copiedAtMs;
        object pinyinBlob;

        using (var select = CreateCommand(conn, tx,
            """
            SELECT entry_type, text_content, image_blob, image_w, image_h, file_paths_json, copied_at_ms, pinyin_blob
            FROM clipboard_history
            WHERE id = @id
            """))
        {
            select.Parameters.AddWithValue("@id", id);
            using var r = select.ExecuteReader();
            if (!r.Read()) return false;

            entryType = r.GetInt32(0);
            textContent = r.IsDBNull(1) ? DBNull.Value : r.GetString(1);
            imageBlob = r.IsDBNull(2) ? DBNull.Value : (byte[])r.GetValue(2);
            imageW = r.IsDBNull(3) ? 0 : r.GetInt32(3);
            imageH = r.IsDBNull(4) ? 0 : r.GetInt32(4);
            filePathsJson = r.IsDBNull(5) ? DBNull.Value : r.GetString(5);
            copiedAtMs = r.GetInt64(6);
            pinyinBlob = r.IsDBNull(7) ? DBNull.Value : r.GetString(7);
        }

        var bucketNo = GetWritableArchiveBucket(conn, tx);
        var table = ArchiveTableName(bucketNo);
        var archivedAtMs = NowMs();

        using (var insert = CreateCommand(conn, tx,
            $"""
             INSERT INTO {table}
               (original_id, entry_type, text_content, image_blob, image_w, image_h, file_paths_json, copied_at_ms, pinyin_blob, archived_at_ms)
             VALUES
               (@originalId, @entryType, @textContent, @imageBlob, @imageW, @imageH, @filePathsJson, @copiedAtMs, @pinyinBlob, @archivedAtMs)
             """))
        {
            insert.Parameters.AddWithValue("@originalId", id);
            insert.Parameters.AddWithValue("@entryType", entryType);
            insert.Parameters.AddWithValue("@textContent", textContent);
            insert.Parameters.AddWithValue("@imageBlob", imageBlob);
            insert.Parameters.AddWithValue("@imageW", imageW);
            insert.Parameters.AddWithValue("@imageH", imageH);
            insert.Parameters.AddWithValue("@filePathsJson", filePathsJson);
            insert.Parameters.AddWithValue("@copiedAtMs", copiedAtMs);
            insert.Parameters.AddWithValue("@pinyinBlob", pinyinBlob);
            insert.Parameters.AddWithValue("@archivedAtMs", archivedAtMs);
            insert.ExecuteNonQuery();
        }

        using (var update = CreateCommand(conn, tx,
            """
            UPDATE clipboard_history_archive_buckets
            SET item_count = item_count + 1,
                min_copied_at_ms = CASE
                  WHEN min_copied_at_ms IS NULL OR @copiedAtMs < min_copied_at_ms THEN @copiedAtMs
                  ELSE min_copied_at_ms
                END,
                max_copied_at_ms = CASE
                  WHEN max_copied_at_ms IS NULL OR @copiedAtMs > max_copied_at_ms THEN @copiedAtMs
                  ELSE max_copied_at_ms
                END,
                sealed_at_ms = CASE
                  WHEN item_count + 1 >= @maxItems THEN @archivedAtMs
                  ELSE sealed_at_ms
                END
            WHERE bucket_no = @bucketNo
            """))
        {
            update.Parameters.AddWithValue("@copiedAtMs", copiedAtMs);
            update.Parameters.AddWithValue("@archivedAtMs", archivedAtMs);
            update.Parameters.AddWithValue("@maxItems", ArchiveBucketMaxItems);
            update.Parameters.AddWithValue("@bucketNo", bucketNo);
            update.ExecuteNonQuery();
        }

        using (var delete = CreateCommand(conn, tx, "DELETE FROM clipboard_history WHERE id = @id"))
        {
            delete.Parameters.AddWithValue("@id", id);
            delete.ExecuteNonQuery();
        }

        return true;
    }

}

