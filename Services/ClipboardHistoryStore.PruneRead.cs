using System.IO;
using System.Text.Json;
using ClipboardManager.Models;
using ClipboardManager.Services;
using Microsoft.Data.Sqlite;

namespace ClipboardManager;

internal sealed partial class ClipboardHistoryStore
{    public void PruneExcess(int maxKeep)
    {
        if (maxKeep < 0) return;
        try
        {
            using var conn = Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText =
                """
                DELETE FROM clipboard_history
                WHERE is_starred = 0
                  AND COALESCE(shortcut_phrase, '') = ''
                  AND id NOT IN (
                    SELECT id FROM clipboard_history
                    WHERE is_starred = 0
                      AND COALESCE(shortcut_phrase, '') = ''
                    ORDER BY copied_at_ms DESC
                    LIMIT @lim
                  );
                """;
            cmd.Parameters.AddWithValue("@lim", maxKeep);
            cmd.ExecuteNonQuery();
        }
        catch
        {
            // ignore
        }
    }

    /// <summary>容量裁剪专用：超出热区上限的未收藏旧项移入冷桶，再从热表删除。</summary>
    public void ArchiveExcess(int maxKeep)
    {
        if (maxKeep < 0) return;
        try
        {
            using var conn = Open();
            using var tx = conn.BeginTransaction();

            var ids = new List<long>();
            using (var select = CreateCommand(conn, tx,
                """
                SELECT id
                FROM clipboard_history
                WHERE is_starred = 0
                  AND COALESCE(shortcut_phrase, '') = ''
                  AND id NOT IN (
                    SELECT id FROM clipboard_history
                    WHERE is_starred = 0
                      AND COALESCE(shortcut_phrase, '') = ''
                    ORDER BY copied_at_ms DESC, id DESC
                    LIMIT @lim
                  )
                ORDER BY copied_at_ms ASC, id ASC
                """))
            {
                select.Parameters.AddWithValue("@lim", maxKeep);
                using var r = select.ExecuteReader();
                while (r.Read())
                    ids.Add(r.GetInt64(0));
            }

            foreach (var id in ids)
                ArchiveAndDeleteCore(conn, tx, id);

            tx.Commit();
        }
        catch
        {
            // ignore
        }
    }

    private static ClipboardEntry ReadEntry(SqliteDataReader r)
    {
        var entry = new ClipboardEntry
        {
            PersistedId = r.GetInt64(0),
            Type = (EntryType)r.GetInt32(1),
            CopiedAt = FromMs(r.GetInt64(7)),
            IsQuickPaste = false,
            IsStarred = r.FieldCount > 17 && !r.IsDBNull(17) && r.GetInt64(17) != 0,
            ShortcutPhrase = r.FieldCount > 18 && !r.IsDBNull(18) ? r.GetString(18) : null
        };
        if (!r.IsDBNull(2)) entry.TextContent = r.GetString(2);
        if (!r.IsDBNull(3)) entry.ImageData = (byte[])r.GetValue(3);
        entry.ImageWidth = r.IsDBNull(4) ? 0 : r.GetInt32(4);
        entry.ImageHeight = r.IsDBNull(5) ? 0 : r.GetInt32(5);
        if (!r.IsDBNull(6))
        {
            var json = r.GetString(6);
            entry.FilePaths = JsonSerializer.Deserialize<string[]>(json) ?? [];
        }
        var source = new ClipboardSourceInfo
        {
            AppName = r.FieldCount > 8 && !r.IsDBNull(8) ? r.GetString(8) : null,
            ExeName = r.FieldCount > 9 && !r.IsDBNull(9) ? r.GetString(9) : null,
            ExePath = r.FieldCount > 10 && !r.IsDBNull(10) ? r.GetString(10) : null,
            WindowTitle = r.FieldCount > 11 && !r.IsDBNull(11) ? r.GetString(11) : null,
            WindowClass = r.FieldCount > 12 && !r.IsDBNull(12) ? r.GetString(12) : null,
            FocusedClass = r.FieldCount > 13 && !r.IsDBNull(13) ? r.GetString(13) : null,
            ProcessId = r.FieldCount > 14 && !r.IsDBNull(14) ? unchecked((uint)r.GetInt64(14)) : 0,
            Hwnd = r.FieldCount > 15 && !r.IsDBNull(15) ? r.GetInt64(15) : 0,
            CaptureMethod = r.FieldCount > 16 && !r.IsDBNull(16) ? r.GetString(16) : null
        };
        if (source.HasAny)
            entry.Source = source;
        if (r.FieldCount > 20 && !r.IsDBNull(20))
            entry.OcrText = r.GetString(20);
        return entry;
    }

    public byte[]? LoadImageData(long persistedId)
    {
        if (persistedId <= 0) return null;
        try
        {
            using var conn = Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT image_blob FROM clipboard_history WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", persistedId);
            return cmd.ExecuteScalar() as byte[];
        }
        catch
        {
            return null;
        }
    }

    public List<long> PruneExcessImages(int maxImageKeep)
    {
        var deleted = new List<long>();
        if (maxImageKeep < 0) return deleted;
        try
        {
            using var conn = Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText =
                """
                SELECT id FROM clipboard_history
                WHERE entry_type = @type
                  AND is_starred = 0
                  AND COALESCE(shortcut_phrase, '') = ''
                ORDER BY copied_at_ms DESC, id DESC
                LIMIT -1 OFFSET @limit
                """;
            cmd.Parameters.AddWithValue("@type", (int)EntryType.Image);
            cmd.Parameters.AddWithValue("@limit", maxImageKeep);
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read()) deleted.Add(reader.GetInt64(0));
            }

            if (deleted.Count > 0)
            {
                using var delete = conn.CreateCommand();
                delete.CommandText = $"DELETE FROM clipboard_history WHERE id IN ({string.Join(',', deleted)})";
                delete.ExecuteNonQuery();
            }
        }
        catch
        {
            deleted.Clear();
        }
        return deleted;
    }

}

