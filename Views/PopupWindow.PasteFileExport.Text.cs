using System.IO;
using System.Text;

namespace ClipboardManager;

public partial class PopupWindow
{
    private async void PasteTextAsFileForExplorer()
    {
        if (ItemsList.SelectedItem is not ClipboardEntry item || item.Type != EntryType.Text) return;
        var text = item.TextContent;
        if (string.IsNullOrEmpty(text) || IsWellFormedJson(text)) return;

        _pasteInProgress = true;
        try
        {
            ClearPendingDelete();
            if (!item.IsQuickPaste)
            {
                var index = _allItems.IndexOf(item);
                if (index > 0)
                {
                    _allItems.RemoveAt(index);
                    _allItems.Insert(0, item);
                }
                item.TouchCopiedTime();
                if (item.PersistedId is long id)
                    _historyStore.TryUpdateCopiedAt(id, item.CopiedAt);
            }

            var directory = Path.Combine(Path.GetTempPath(), "ClipboardX");
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, $"clip_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
            File.WriteAllText(path, text, new UTF8Encoding(false));
            await CompletePasteTempFileToExplorerAsync(
                path,
                $"textChars={text.Length}",
                $"explorer_temp_txt file=\"{path}\"");
        }
        catch (Exception ex)
        {
            ClipboardDiagnosticsLog.Write($"paste text as file failed: {ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            _pasteInProgress = false;
        }
    }
}
