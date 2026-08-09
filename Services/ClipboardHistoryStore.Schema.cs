using System.IO;
using System.Text.Json;
using ClipboardManager.Models;
using ClipboardManager.Services;
using Microsoft.Data.Sqlite;

namespace ClipboardManager;

internal sealed partial class ClipboardHistoryStore
{
    private const int SearchSchemaVersion = 2;

    private void EnsureSchema()
    {
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();

        try
        {
            cmd.CommandText = "ALTER TABLE clipboard_history ADD COLUMN pinyin_blob TEXT;";
            cmd.ExecuteNonQuery();
        }
        catch
        {
            // Column likely exists, ignore
        }

        try
        {
            cmd.CommandText = "ALTER TABLE clipboard_history ADD COLUMN is_starred INTEGER NOT NULL DEFAULT 0;";
            cmd.ExecuteNonQuery();
        }
        catch
        {
            // Column likely exists, ignore
        }

        try
        {
            cmd.CommandText = "ALTER TABLE clipboard_history ADD COLUMN shortcut_phrase TEXT;";
            cmd.ExecuteNonQuery();
        }
        catch
        {
            // Column likely exists, ignore
        }

        foreach (var column in new[]
                 {
                     "source_app_name TEXT",
                     "source_exe_name TEXT",
                     "source_exe_path TEXT",
                     "source_window_title TEXT",
                     "source_window_class TEXT",
                     "source_focused_class TEXT",
                     "source_process_id INTEGER NOT NULL DEFAULT 0",
                     "source_hwnd INTEGER NOT NULL DEFAULT 0",
                     "source_capture_method TEXT",
                     "source_search_text TEXT",
                     "ocr_text TEXT"
                 })
        {
            try
            {
                cmd.CommandText = $"ALTER TABLE clipboard_history ADD COLUMN {column};";
                cmd.ExecuteNonQuery();
            }
            catch
            {
                // Column likely exists, ignore
            }
        }

        cmd.CommandText =
            """
            PRAGMA journal_mode = WAL;
            CREATE TABLE IF NOT EXISTS clipboard_history (
              id INTEGER PRIMARY KEY AUTOINCREMENT,
              entry_type INTEGER NOT NULL,
              text_content TEXT,
              image_blob BLOB,
              image_w INTEGER NOT NULL DEFAULT 0,
              image_h INTEGER NOT NULL DEFAULT 0,
              file_paths_json TEXT,
              copied_at_ms INTEGER NOT NULL,
              pinyin_blob TEXT,
              source_app_name TEXT,
              source_exe_name TEXT,
              source_exe_path TEXT,
              source_window_title TEXT,
              source_window_class TEXT,
              source_focused_class TEXT,
              source_process_id INTEGER NOT NULL DEFAULT 0,
              source_hwnd INTEGER NOT NULL DEFAULT 0,
              source_capture_method TEXT,
              source_search_text TEXT,
              ocr_text TEXT,
              shortcut_phrase TEXT,
              is_starred INTEGER NOT NULL DEFAULT 0
            );
            CREATE INDEX IF NOT EXISTS idx_clipboard_history_copied ON clipboard_history(copied_at_ms DESC);

            CREATE TABLE IF NOT EXISTS app_icon_cache (
              icon_key TEXT PRIMARY KEY,
              exe_path TEXT NOT NULL,
              file_mtime_utc_ticks INTEGER NOT NULL,
              file_size INTEGER NOT NULL,
              icon_png BLOB NOT NULL,
              last_seen_ms INTEGER NOT NULL
            );

            CREATE TABLE IF NOT EXISTS clipboard_history_archive_buckets (
              bucket_no INTEGER PRIMARY KEY,
              table_name TEXT NOT NULL UNIQUE,
              item_count INTEGER NOT NULL DEFAULT 0,
              min_copied_at_ms INTEGER,
              max_copied_at_ms INTEGER,
              created_at_ms INTEGER NOT NULL,
              sealed_at_ms INTEGER
            );

            """;
        cmd.ExecuteNonQuery();

        EnsureSearchSchema(conn);
    }

    private static void EnsureSearchSchema(SqliteConnection conn)
    {
        using var version = conn.CreateCommand();
        version.CommandText = "PRAGMA user_version";
        var currentVersion = Convert.ToInt32(version.ExecuteScalar());
        var rebuild = currentVersion < SearchSchemaVersion;

        using var tx = conn.BeginTransaction();
        if (rebuild)
        {
            using var drop = CreateCommand(conn, tx,
                """
                DROP TRIGGER IF EXISTS t_clipboard_history_ai;
                DROP TRIGGER IF EXISTS t_clipboard_history_ad;
                DROP TRIGGER IF EXISTS t_clipboard_history_au;
                DROP TABLE IF EXISTS clipboard_history_fts;
                """);
            drop.ExecuteNonQuery();
        }

        using (var create = CreateCommand(conn, tx,
            """
            CREATE VIRTUAL TABLE IF NOT EXISTS clipboard_history_fts USING fts5(
              text_content,
              file_paths_json,
              shortcut_phrase,
              pinyin_blob,
              source_search_text,
              content='clipboard_history',
              content_rowid='id',
              tokenize='trigram'
            );

            CREATE TRIGGER IF NOT EXISTS t_clipboard_history_ai AFTER INSERT ON clipboard_history BEGIN
              INSERT INTO clipboard_history_fts(rowid, text_content, file_paths_json, shortcut_phrase, pinyin_blob, source_search_text)
              VALUES (new.id, new.text_content, new.file_paths_json, new.shortcut_phrase, new.pinyin_blob, new.source_search_text);
            END;
            CREATE TRIGGER IF NOT EXISTS t_clipboard_history_ad AFTER DELETE ON clipboard_history BEGIN
              INSERT INTO clipboard_history_fts(clipboard_history_fts, rowid, text_content, file_paths_json, shortcut_phrase, pinyin_blob, source_search_text)
              VALUES ('delete', old.id, old.text_content, old.file_paths_json, old.shortcut_phrase, old.pinyin_blob, old.source_search_text);
            END;
            CREATE TRIGGER IF NOT EXISTS t_clipboard_history_au AFTER UPDATE ON clipboard_history BEGIN
              INSERT INTO clipboard_history_fts(clipboard_history_fts, rowid, text_content, file_paths_json, shortcut_phrase, pinyin_blob, source_search_text)
              VALUES ('delete', old.id, old.text_content, old.file_paths_json, old.shortcut_phrase, old.pinyin_blob, old.source_search_text);
              INSERT INTO clipboard_history_fts(rowid, text_content, file_paths_json, shortcut_phrase, pinyin_blob, source_search_text)
              VALUES (new.id, new.text_content, new.file_paths_json, new.shortcut_phrase, new.pinyin_blob, new.source_search_text);
            END;
            """))
        {
            create.ExecuteNonQuery();
        }

        if (rebuild)
        {
            using var populate = CreateCommand(conn, tx,
                """
                INSERT INTO clipboard_history_fts(rowid, text_content, file_paths_json, shortcut_phrase, pinyin_blob, source_search_text)
                SELECT id, text_content, file_paths_json, shortcut_phrase, pinyin_blob, source_search_text
                FROM clipboard_history;
                """);
            populate.ExecuteNonQuery();

            using var setVersion = CreateCommand(conn, tx, $"PRAGMA user_version = {SearchSchemaVersion}");
            setVersion.ExecuteNonQuery();
        }

        tx.Commit();

        var archiveBuckets = new List<int>();
        using (var buckets = conn.CreateCommand())
        {
            buckets.CommandText = "SELECT bucket_no FROM clipboard_history_archive_buckets ORDER BY bucket_no";
            using var reader = buckets.ExecuteReader();
            while (reader.Read())
                archiveBuckets.Add(reader.GetInt32(0));
        }

        foreach (var bucketNo in archiveBuckets)
            EnsureArchiveBucketTable(conn, null, bucketNo);
    }

    private SqliteConnection Open()
    {
        var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        return conn;
    }

    private static long ToMs(DateTime dt) => new DateTimeOffset(dt).ToUnixTimeMilliseconds();

    private static DateTime FromMs(long ms) => DateTimeOffset.FromUnixTimeMilliseconds(ms).LocalDateTime;

    private static string ArchiveTableName(int bucketNo) => $"clipboard_history_archive_{bucketNo:0000}";

    private static long NowMs() => DateTimeOffset.Now.ToUnixTimeMilliseconds();

    private static SqliteCommand CreateCommand(SqliteConnection conn, SqliteTransaction? tx, string commandText)
    {
        var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = commandText;
        return cmd;
    }
}

