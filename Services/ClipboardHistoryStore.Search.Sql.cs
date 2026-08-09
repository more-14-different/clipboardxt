using ClipboardManager.Models;
using Microsoft.Data.Sqlite;

namespace ClipboardManager;

internal sealed partial class ClipboardHistoryStore
{
    private static SqliteCommand CreateCandidateCommand(
        SqliteConnection conn,
        SearchQuerySpec spec,
        EntryType? typeFilter,
        bool shortcutPhraseOnly,
        int pageSize,
        int offset)
    {
        var whereClauses = new List<string>();
        AddSearchTokenClauses(spec, whereClauses);

        if (typeFilter.HasValue)
        {
            whereClauses.Add(typeFilter.Value == EntryType.Image
                ? BuildImageTypeClause()
                : "c.entry_type = @typeFilter");
        }

        if (shortcutPhraseOnly)
            whereClauses.Add("COALESCE(c.shortcut_phrase, '') <> ''");

        var sql =
            """
            SELECT c.id, c.entry_type, c.text_content, NULL AS image_blob, c.image_w, c.image_h,
                   c.file_paths_json, c.copied_at_ms,
                   c.source_app_name, c.source_exe_name, c.source_exe_path, c.source_window_title,
                   c.source_window_class, c.source_focused_class, c.source_process_id,
                   c.source_hwnd, c.source_capture_method, c.is_starred, c.shortcut_phrase,
                   c.pinyin_blob, c.ocr_text
            FROM clipboard_history c
            """;

        if (whereClauses.Count > 0)
            sql += " WHERE " + string.Join(" AND ", whereClauses);

        sql += " ORDER BY c.is_starred DESC, c.copied_at_ms DESC, c.id DESC LIMIT @pageSize OFFSET @offset";

        var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        AddSearchTokenParameters(cmd, spec);
        if (typeFilter.HasValue && typeFilter.Value != EntryType.Image)
            cmd.Parameters.AddWithValue("@typeFilter", (int)typeFilter.Value);
        cmd.Parameters.AddWithValue("@pageSize", pageSize);
        cmd.Parameters.AddWithValue("@offset", offset);
        return cmd;
    }

    private static void AddSearchTokenClauses(SearchQuerySpec spec, List<string> whereClauses)
    {
        if (spec.AnchorStart && !string.IsNullOrWhiteSpace(spec.StartNeedle))
        {
            whereClauses.Add(
                "(c.text_content LIKE @likeQStart ESCAPE '\\' " +
                "OR c.file_paths_json LIKE @likeQStart ESCAPE '\\' " +
                "OR c.shortcut_phrase LIKE @likeQStart ESCAPE '\\')");
        }

        if (spec.AnchorEnd && !string.IsNullOrWhiteSpace(spec.EndNeedle))
        {
            whereClauses.Add(
                "(c.text_content LIKE @likeQEnd ESCAPE '\\' " +
                "OR c.file_paths_json LIKE @likeQEnd ESCAPE '\\' " +
                "OR c.shortcut_phrase LIKE @likeQEnd ESCAPE '\\')");
        }

        for (var i = 0; i < spec.BroadTokens.Length; i++)
        {
            if (spec.BroadTokens[i].Length >= 3)
            {
                whereClauses.Add(
                    $"c.id IN (SELECT rowid FROM clipboard_history_fts WHERE clipboard_history_fts MATCH @q{i})");
            }
            else
            {
                whereClauses.Add(
                    $"(c.text_content LIKE @likeQ{i} ESCAPE '\\' " +
                    $"OR c.file_paths_json LIKE @likeQ{i} ESCAPE '\\' " +
                    $"OR c.shortcut_phrase LIKE @likeQ{i} ESCAPE '\\' " +
                    $"OR c.pinyin_blob LIKE @likeQ{i} ESCAPE '\\' " +
                    $"OR c.source_search_text LIKE @likeQ{i} ESCAPE '\\')");
            }
        }
    }

    private static void AddSearchTokenParameters(SqliteCommand cmd, SearchQuerySpec spec)
    {
        if (spec.AnchorStart && !string.IsNullOrWhiteSpace(spec.StartNeedle))
            cmd.Parameters.AddWithValue("@likeQStart", $"{EscapeLikePattern(spec.StartNeedle.Trim())}%");

        if (spec.AnchorEnd && !string.IsNullOrWhiteSpace(spec.EndNeedle))
            cmd.Parameters.AddWithValue("@likeQEnd", $"%{EscapeLikePattern(spec.EndNeedle.Trim())}");

        for (var i = 0; i < spec.BroadTokens.Length; i++)
        {
            var token = spec.BroadTokens[i];
            if (token.Length >= 3)
                cmd.Parameters.AddWithValue($"@q{i}", $"\"{token.Replace("\"", "\"\"")}\"");
            else
                cmd.Parameters.AddWithValue($"@likeQ{i}", $"%{EscapeLikePattern(token)}%");
        }
    }

    private static string BuildImageTypeClause()
    {
        var suffixes = new[] { ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp", ".tiff", ".tif", ".ico" };
        var fileClauses = suffixes.Select(
            suffix => $"lower(json_extract(c.file_paths_json, '$[0]')) LIKE '%{suffix}'");
        return $"(c.entry_type = {(int)EntryType.Image} OR " +
               $"(c.entry_type = {(int)EntryType.Files} AND ({string.Join(" OR ", fileClauses)})))";
    }

    private static string EscapeLikePattern(string value) =>
        value.Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_");
}
