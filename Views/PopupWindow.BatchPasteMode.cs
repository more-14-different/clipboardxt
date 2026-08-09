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
    private BatchPasteQueueMode GetBatchMode()
    {
        if (_appSettings == null) return BatchPasteQueueMode.Off;
        return Enum.TryParse<BatchPasteQueueMode>(_appSettings.BatchPasteMode, true, out var m)
            ? m
            : BatchPasteQueueMode.Off;
    }

    private void SetBatchPasteMode(BatchPasteQueueMode mode)
    {
        if (_appSettings == null) return;
#if CLIPX_CLIPBOARD
        _batchQueueAwaitingNextPasteToSwitchOff = false;
#endif
        var prev = GetBatchMode();
        _appSettings.BatchPasteMode = mode.ToString();
        if (mode == BatchPasteQueueMode.Off)
        {
            _batchQueue.Clear();
#if CLIPX_CLIPBOARD
            if (_batchQueueProviderSession != null)
            {
                _ = _batchQueueProviderSession.DisposeAsync();
                _batchQueueProviderSession = null;
            }
#endif
        }
        _appSettings.Save();
        UpdateBatchHeaderUi();
        UpdateBatchOrderProperties();
        RefreshFilter();
#if CLIPX_CLIPBOARD
        SyncBatchPasteKeyboardHook();
#endif
        if (prev != mode)
            BatchPasteModeChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>顺序：普通（关队列）→ LIFO → FIFO → 普通。全局快捷键由 App 侧消息窗口注册，与顶栏左键相同。</summary>
    public void CycleBatchPasteMode()
    {
        var now = Environment.TickCount64;
        if (now - _cycleBatchPasteDebounceTick < 45) return;
        _cycleBatchPasteDebounceTick = now;
        var next = GetBatchMode() switch
        {
            BatchPasteQueueMode.Off => BatchPasteQueueMode.Lifo,
            BatchPasteQueueMode.Lifo => BatchPasteQueueMode.Fifo,
            _ => BatchPasteQueueMode.Off
        };
        SetBatchPasteMode(next);
    }
}
