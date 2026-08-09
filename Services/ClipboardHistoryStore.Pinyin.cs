using System.IO;
using System.Text.Json;
using ClipboardManager.Models;
using ClipboardManager.Services;
using Microsoft.Data.Sqlite;

namespace ClipboardManager;

internal sealed partial class ClipboardHistoryStore
{    public void RebuildPinyinBlobs(string mode)
    {
        PinyinFilterMode = PinyinFilterModes.Normalize(mode);
        try
        {
            using var conn = Open();
            using var tx = conn.BeginTransaction();
            var rows = new List<(long Id, EntryType Type, string? Text, string? FilesJson, int W, int H, string? SourceSearch, string? ShortcutPhrase)>();
            using (var select = CreateCommand(conn, tx,
                """
                SELECT id, entry_type, text_content, file_paths_json, image_w, image_h, source_search_text, shortcut_phrase
                FROM clipboard_history
                """))
            using (var r = select.ExecuteReader())
            {
                while (r.Read())
                {
                    rows.Add((
                        r.GetInt64(0),
                        (EntryType)r.GetInt32(1),
                        r.IsDBNull(2) ? null : r.GetString(2),
                        r.IsDBNull(3) ? null : r.GetString(3),
                        r.IsDBNull(4) ? 0 : r.GetInt32(4),
                        r.IsDBNull(5) ? 0 : r.GetInt32(5),
                        r.IsDBNull(6) ? null : r.GetString(6),
                        r.IsDBNull(7) ? null : r.GetString(7)));
                }
            }

            foreach (var row in rows)
            {
                var searchable = BuildSearchableText(row.Type, row.Text, row.FilesJson, row.W, row.H, row.SourceSearch, row.ShortcutPhrase);
                var py = PinyinSearchIndex.BuildBlob(searchable, PinyinFilterMode);
                using var update = CreateCommand(conn, tx, "UPDATE clipboard_history SET pinyin_blob = @py WHERE id = @id");
                update.Parameters.AddWithValue("@py", py);
                update.Parameters.AddWithValue("@id", row.Id);
                update.ExecuteNonQuery();
            }

            tx.Commit();
        }
        catch
        {
            // ignore
        }
    }

    private static string BuildSearchableText(EntryType type, string? text, string? filesJson, int imageWidth, int imageHeight, string? sourceSearchText = null, string? shortcutPhrase = null)
    {
        var baseText = type switch
        {
            EntryType.Text => text ?? "",
            EntryType.Files => string.Join(" ", TryDeserializeFileNames(filesJson)),
            EntryType.Image => $"image 图片 {imageWidth}x{imageHeight}",
            _ => ""
        };
        if (!string.IsNullOrWhiteSpace(shortcutPhrase))
            baseText = $"{shortcutPhrase} {baseText}";
        return string.IsNullOrWhiteSpace(sourceSearchText) ? baseText : $"{baseText} {sourceSearchText}";
    }

    private static IEnumerable<string> TryDeserializeFileNames(string? filesJson)
    {
        if (string.IsNullOrWhiteSpace(filesJson)) return [];
        try
        {
            return (JsonSerializer.Deserialize<string[]>(filesJson) ?? []).Select(Path.GetFileName).OfType<string>();
        }
        catch
        {
            return [];
        }
    }
}

