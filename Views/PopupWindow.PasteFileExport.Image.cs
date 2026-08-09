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
using ClipboardManager.Services;

namespace ClipboardManager;

public partial class PopupWindow : Window
{
    /// <summary>
    /// 将剪贴板图片历史写入临时 PNG 并放到系统剪贴板为文件列表，便于在资源管理器中 Ctrl+V 直接保存文件。
    /// </summary>
    private async void PasteImageAsFileForExplorer()
    {
        if (ItemsList.SelectedItem is not ClipboardEntry item || item.Type != EntryType.Image) return;
        var imageData = item.TryGetImageData();
        if (imageData is not { Length: > 0 }) return;
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
        try
        {
            Directory.CreateDirectory(dir);
        }
        catch { return; }

        var path = Path.Combine(dir, $"clip_{DateTime.Now:yyyyMMdd_HHmmss}.png");
        try
        {
            File.WriteAllBytes(path, ClipboardImageCodec.NormalizePngBytes(imageData));
        }
        catch { return; }

        await CompletePasteTempFileToExplorerAsync(
            path,
            $"pngBytes={imageData.Length}",
            $"SetFileDropList explorer_temp_png file=\"{path}\"");
        }
        finally
        {
            _pasteInProgress = false;
        }
    }
}
