using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Brush = System.Windows.Media.Brush;
using Button = System.Windows.Controls.Button;
using Orientation = System.Windows.Controls.Orientation;
using TextBox = System.Windows.Controls.TextBox;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using ClipboardManager.Models;

namespace ClipboardManager;

public partial class PopupWindow : Window
{
    private static bool IsWellFormedJson(string? text) =>
        ClipboardFileExportPlanner.IsWellFormedJson(text);

    /// <summary>
    /// 文本为合法 JSON 时写入临时 .json 文件并置于剪贴板文件列表，在资源管理器中粘贴即可落盘。
    /// </summary>
    private async void PasteJsonAsFileForExplorer()
    {
        if (ItemsList.SelectedItem is not ClipboardEntry item || item.Type != EntryType.Text) return;
        var text = item.TextContent;
        if (string.IsNullOrWhiteSpace(text) || !IsWellFormedJson(text)) return;

        _pasteInProgress = true;
        try
        {
        ClearPendingDelete();

        if (!item.IsQuickPaste)
        {
            var idx = _allItems.IndexOf(item);
            if (idx > 0) { _allItems.RemoveAt(idx); _allItems.Insert(0, item); }
            item.TouchCopiedTime();
            if (item.PersistedId is long pid)
                _historyStore.TryUpdateCopiedAt(pid, item.CopiedAt);
        }

        var dir = Path.Combine(Path.GetTempPath(), "ClipboardX");
        try { Directory.CreateDirectory(dir); }
        catch { return; }

        var path = Path.Combine(dir, $"clip_{DateTime.Now:yyyyMMdd_HHmmss}.json");
        try
        {
            File.WriteAllText(path, text, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }
        catch { return; }

        await CompletePasteTempFileToExplorerAsync(
            path,
            $"jsonChars={text.Length}",
            $"SetFileDropList explorer_temp_json file=\"{path}\"");
        }
        finally
        {
            _pasteInProgress = false;
        }
    }
}
