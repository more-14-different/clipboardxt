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
#if CLIPX_CLIPBOARD
    /// <summary>面板显示、FIFO/LIFO 且队列非空、或「队已贴完等下一次粘键切回普通」、或启用了替换系统 Win+V 时需 WH_KEYBOARD_LL。</summary>
    private void SyncBatchPasteKeyboardHook()
    {
        var need = _isPopupVisible
            || (GetBatchMode() != BatchPasteQueueMode.Off && _batchQueue.Count > 0)
            || _awaitHotkeyAltChordCleanup
            || _batchQueueAwaitingNextPasteToSwitchOff
            || (_appSettings?.ReplaceSystemWinV ?? false);
        if (need)
            InstallKeyboardHook();
        else
            UninstallKeyboardHook();
    }
#else
    private void SyncBatchPasteKeyboardHook() { }
#endif

    /// <summary>热键 Alt 收尾窗口超时后卸下钩子（若不再需要）。</summary>
    private void TryExpireHotkeyAltChordCleanupDeadline()
    {
        if (!_awaitHotkeyAltChordCleanup || _hotkeyAltChordCleanupDeadlineTick == 0)
            return;
        if (Environment.TickCount64 <= _hotkeyAltChordCleanupDeadlineTick)
            return;
        _awaitHotkeyAltChordCleanup = false;
        _hotkeyAltChordCleanupDeadlineTick = 0;
#if CLIPX_CLIPBOARD
        SyncBatchPasteKeyboardHook();
#else
        if (!_isPopupVisible)
            UninstallKeyboardHook();
#endif
    }
}
