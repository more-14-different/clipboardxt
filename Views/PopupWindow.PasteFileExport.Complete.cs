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
    /// <summary>
    /// 将临时文件路径写入剪贴板文件列表并模拟粘贴，供资源管理器接收。
    /// </summary>
    private async Task CompletePasteTempFileToExplorerAsync(string path, string beginLogDetail, string setClipboardLogOp)
    {
        if (_targetWindow != IntPtr.Zero && !Win32.IsWindow(_targetWindow))
            _targetWindow = IntPtr.Zero;

        ClipboardDiagnosticsLog.Write(
            $"pasteAsFile BEGIN {beginLogDetail} temp=\"{path}\" target=0x{_targetWindow.ToInt64():X}");

        if (!_popupPinned)
            HidePopup();
        if (_targetWindow != IntPtr.Zero)
            Win32.SetForegroundWindowAggressive(_targetWindow);
        await Task.Delay(85);
        await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);

        if (_hwnd != IntPtr.Zero)
            Win32.TryEmptyClipboardAfterOpen(_hwnd);

        _isSettingClipboard = true;
        var nativeOk = await NativeClipboardWriteRetry.TrySetAsync(
            () => Win32.TrySetClipboardFileDropListNative(new[] { path }, _hwnd),
            setClipboardLogOp,
            _hwnd,
            maxRetries: 8,
            baseDelayMs: 60);
        var fl = new StringCollection();
        fl.Add(path);
        bool clipboardOk = nativeOk || await ClipboardWriteRetry.TrySetAsync(
            () => System.Windows.Clipboard.SetFileDropList(fl),
            setClipboardLogOp,
            clipNudgeHwnd: _hwnd);

        ClipboardDiagnosticsLog.Write($"pasteAsFile END clipboardOk={clipboardOk}");

        if (!clipboardOk)
        {
            _isSettingClipboard = false;
            try { File.Delete(path); } catch { /* ignore */ }
        }
        else
        {
            _ = Dispatcher.BeginInvoke(DispatcherPriority.SystemIdle, () => _isSettingClipboard = false);
        }

        if (clipboardOk)
        {
            await Task.Delay(60);
            SendPasteToTarget();
            await Task.Delay(600);
            ClipboardDiagnosticsLog.Write("pasteAsFile post-echo suppression window elapsed");
        }
    }
}
