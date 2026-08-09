using System.IO;
using System.Text.Json;
using ClipboardManager.Models;
using ClipboardManager.Services;
using Microsoft.Data.Sqlite;

namespace ClipboardManager;

internal sealed partial class ClipboardHistoryStore
{    public bool TryInsert(ClipboardEntry entry)
    {
        if (entry.IsQuickPaste) return false;
        try
        {
            var ms = ToMs(entry.CopiedAt);
            string? filesJson = entry.Type == EntryType.Files && entry.FilePaths is { Length: > 0 }
                ? JsonSerializer.Serialize(entry.FilePaths)
                : null;
                
            var pinyinBlob = PinyinSearchIndex.BuildBlob(entry.FullSearchableText, PinyinFilterMode);
            var sourceSearchText = entry.SourceSearchText;

            using var conn = Open();
            entry.SourceIconPng ??= SourceAppIconCache.GetOrCreateIconPng(entry.Source?.ExePath, conn);
            using var cmd = conn.CreateCommand();
            cmd.CommandText =
                """
                INSERT INTO clipboard_history
                  (entry_type, text_content, image_blob, image_w, image_h, file_paths_json, copied_at_ms, pinyin_blob,
                   source_app_name, source_exe_name, source_exe_path, source_window_title, source_window_class,
                   source_focused_class, source_process_id, source_hwnd, source_capture_method, source_search_text, shortcut_phrase,
                   is_starred, ocr_text)
                VALUES
                  (@t, @text, @blob, @w, @h, @files, @ms, @py,
                   @sourceApp, @sourceExe, @sourcePath, @sourceTitle, @sourceClass,
                   @sourceFocusedClass, @sourcePid, @sourceHwnd, @sourceMethod, @sourceSearch, @shortcutPhrase,
                   @starred, @ocr)
                """;
            cmd.Parameters.AddWithValue("@t", (int)entry.Type);
            cmd.Parameters.AddWithValue("@text", (object?)entry.TextContent ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@blob", (object?)entry.ImageData ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@w", entry.ImageWidth);
            cmd.Parameters.AddWithValue("@h", entry.ImageHeight);
            cmd.Parameters.AddWithValue("@files", (object?)filesJson ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@ms", ms);
            cmd.Parameters.AddWithValue("@py", pinyinBlob);
            cmd.Parameters.AddWithValue("@sourceApp", (object?)entry.Source?.AppName ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@sourceExe", (object?)entry.Source?.ExeName ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@sourcePath", (object?)entry.Source?.ExePath ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@sourceTitle", (object?)entry.Source?.WindowTitle ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@sourceClass", (object?)entry.Source?.WindowClass ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@sourceFocusedClass", (object?)entry.Source?.FocusedClass ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@sourcePid", entry.Source?.ProcessId ?? 0);
            cmd.Parameters.AddWithValue("@sourceHwnd", entry.Source?.Hwnd ?? 0);
            cmd.Parameters.AddWithValue("@sourceMethod", (object?)entry.Source?.CaptureMethod ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@sourceSearch", string.IsNullOrWhiteSpace(sourceSearchText) ? DBNull.Value : sourceSearchText);
            cmd.Parameters.AddWithValue("@shortcutPhrase", (object?)entry.ShortcutPhrase ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@starred", entry.IsStarred ? 1 : 0);
            cmd.Parameters.AddWithValue("@ocr", (object?)entry.OcrText ?? DBNull.Value);
            cmd.ExecuteNonQuery();
            cmd.CommandText = "SELECT last_insert_rowid()";
            var idObj = cmd.ExecuteScalar();
            if (idObj is long lid) entry.PersistedId = lid;
            else if (idObj != null) entry.PersistedId = Convert.ToInt64(idObj);
            return entry.PersistedId.HasValue;
        }
        catch
        {
            return false;
        }
    }

    public void TryDelete(long? persistedId)
    {
        if (persistedId is not long id || id <= 0) return;
        try
        {
            using var conn = Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM clipboard_history WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }
        catch
        {
            // ignore
        }
    }

    /// <summary>容量裁剪专用：将单条热数据移入冷桶，再从热表删除。</summary>
    public void TryArchiveAndDelete(long? persistedId)
    {
        if (persistedId is not long id || id <= 0) return;
        try
        {
            using var conn = Open();
            using var tx = conn.BeginTransaction();
            ArchiveAndDeleteCore(conn, tx, id);
            tx.Commit();
        }
        catch
        {
            // ignore
        }
    }

    public void TryUpdateCopiedAt(long persistedId, DateTime copiedAt)
    {
        if (persistedId <= 0) return;
        try
        {
            using var conn = Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE clipboard_history SET copied_at_ms = @ms WHERE id = @id";
            cmd.Parameters.AddWithValue("@ms", ToMs(copiedAt));
            cmd.Parameters.AddWithValue("@id", persistedId);
            cmd.ExecuteNonQuery();
        }
        catch
        {
            // ignore
        }
    }

    /// <summary>就地更新文本条目的内容（entry_type 仍为文本）。</summary>
    public void TryUpdateText(long persistedId, string text)
    {
        if (persistedId <= 0) return;
        try
        {
            using var conn = Open();
            string? shortcutPhrase = null;
            using (var read = conn.CreateCommand())
            {
                read.CommandText = "SELECT shortcut_phrase FROM clipboard_history WHERE id = @id";
                read.Parameters.AddWithValue("@id", persistedId);
                shortcutPhrase = read.ExecuteScalar() as string;
            }
            var pySource = string.IsNullOrWhiteSpace(shortcutPhrase) ? text : $"{shortcutPhrase} {text}";
            var py = PinyinSearchIndex.BuildBlob(pySource, PinyinFilterMode);
            using var cmd = conn.CreateCommand();
            cmd.CommandText =
                "UPDATE clipboard_history SET text_content = @t, pinyin_blob = @py WHERE id = @id AND entry_type = @et";
            cmd.Parameters.AddWithValue("@t", text);
            cmd.Parameters.AddWithValue("@py", py);
            cmd.Parameters.AddWithValue("@id", persistedId);
            cmd.Parameters.AddWithValue("@et", (int)EntryType.Text);
            cmd.ExecuteNonQuery();
        }
        catch
        {
            // ignore
        }
    }

    public void TryUpdateStarred(long persistedId, bool isStarred)
    {
        if (persistedId <= 0) return;
        try
        {
            using var conn = Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE clipboard_history SET is_starred = @starred WHERE id = @id";
            cmd.Parameters.AddWithValue("@starred", isStarred ? 1 : 0);
            cmd.Parameters.AddWithValue("@id", persistedId);
            cmd.ExecuteNonQuery();
        }
        catch
        {
            // ignore
        }
    }

    public void TryUpdateOcrText(long persistedId, string? ocrText)
    {
        if (persistedId <= 0) return;
        try
        {
            using var conn = Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText =
                """
                UPDATE clipboard_history
                SET ocr_text = @ocr,
                    pinyin_blob = trim(COALESCE(pinyin_blob, '') || ' ' || @ocrPinyin)
                WHERE id = @id AND entry_type = @type
                """;
            cmd.Parameters.AddWithValue("@ocr", (object?)ocrText ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@ocrPinyin", PinyinSearchIndex.BuildBlob(ocrText ?? "", PinyinFilterMode));
            cmd.Parameters.AddWithValue("@id", persistedId);
            cmd.Parameters.AddWithValue("@type", (int)EntryType.Image);
            cmd.ExecuteNonQuery();
        }
        catch
        {
            // ignore
        }
    }

    public void TryUpdateShortcutPhrase(long persistedId, string? shortcutPhrase)
    {
        if (persistedId <= 0) return;
        try
        {
            shortcutPhrase = string.IsNullOrWhiteSpace(shortcutPhrase) ? null : shortcutPhrase.Trim();
            using var conn = Open();
            ClipboardEntry? entry = null;
            using (var select = conn.CreateCommand())
            {
                select.CommandText =
                    """
                    SELECT c.id, c.entry_type, c.text_content, c.image_blob, c.image_w, c.image_h,
                           c.file_paths_json, c.copied_at_ms,
                           c.source_app_name, c.source_exe_name, c.source_exe_path, c.source_window_title,
                           c.source_window_class, c.source_focused_class, c.source_process_id,
                           c.source_hwnd, c.source_capture_method, c.is_starred, c.shortcut_phrase
                    FROM clipboard_history c
                    WHERE c.id = @id
                    """;
                select.Parameters.AddWithValue("@id", persistedId);
                using var r = select.ExecuteReader();
                if (r.Read())
                    entry = ReadEntry(r);
            }

            if (entry == null) return;
            entry.ShortcutPhrase = shortcutPhrase;
            var py = PinyinSearchIndex.BuildBlob(entry.FullSearchableText, PinyinFilterMode);

            using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE clipboard_history SET shortcut_phrase = @phrase, pinyin_blob = @py WHERE id = @id";
            cmd.Parameters.AddWithValue("@phrase", (object?)shortcutPhrase ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@py", py);
            cmd.Parameters.AddWithValue("@id", persistedId);
            cmd.ExecuteNonQuery();
        }
        catch
        {
            // ignore
        }
    }
}

