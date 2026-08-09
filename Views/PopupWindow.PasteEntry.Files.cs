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
    private async Task<bool> PrepareFilesEntryClipboardAsync(
        ClipboardEntry item,
        int clipRetries,
        int clipRetryDelayMs)
    {
        if (await NativeClipboardWriteRetry.TrySetAsync(
                () => Win32.TrySetClipboardFileDropListNative(item.FilePaths!, _hwnd),
                $"SetFileDropList count={item.FilePaths!.Length}",
                _hwnd,
                clipRetries,
                clipRetryDelayMs))
            return true;

        var fl = new StringCollection();
        fl.AddRange(item.FilePaths!);
        return await ClipboardWriteRetry.TrySetAsync(
            () => System.Windows.Clipboard.SetFileDropList(fl),
            $"SetFileDropList count={fl.Count} {SummarizeFileDropForLog(item.FilePaths!)}",
            maxRetries: clipRetries,
            delayMs: clipRetryDelayMs,
            clipNudgeHwnd: _hwnd);
    }
}
