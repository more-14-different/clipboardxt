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
    /// <summary>FIFO/LIFO：多选 Enter 入队（不立即粘贴），顶栏序号；目标应用内每次 Ctrl+V / Shift+Insert 松键出队并推进剪贴板。</summary>
    private void EnqueueSelectedForBatchPasteMode()
    {
        var mode = GetBatchMode();
        if (mode == BatchPasteQueueMode.Off) return;

        var ordered = ItemsList.SelectedItems.Cast<ClipboardEntry>()
            .Where(e => _displayItems.Contains(e))
            .OrderBy(e => _displayItems.IndexOf(e))
            .ToList();
        if (ordered.Count == 0) return;
        ClearSearchText();

        var headBefore = _batchQueue.Head;
        _batchQueue.Enqueue(ordered, mode);
        UpdateBatchOrderProperties();
        ReorderAllItemsQueueFirst();
        RefreshFilter(0);
        SyncBatchPasteKeyboardHook();
        SchedulePushBatchQueueHeadIfChanged(headBefore, mode);
        if (_batchQueue.Count > 0)
            _batchQueueAwaitingNextPasteToSwitchOff = false;
    }
#endif

    /// <param name="fromClipboardMonitor">为 true 表示 entry 是由 <see cref="OnClipboardUpdate"/> 刚从系统剪贴板读出来的，
    /// 此刻 OS 剪贴板内容 = entry，不需要再回写一次（LIFO 下入队首会触发冗余 SetClipboard，
    /// 与源应用 OLE 通告链争抢导致 8 次×55ms ≈ 440ms 重试卡顿；图片更可能再 fallback 落盘）。</param>
    private void AutoBatchEnqueueIfNeeded(ClipboardEntry entry, bool fromClipboardMonitor = false)
    {
#if CLIPX_CLIPBOARD
        if (entry.IsQuickPaste) return;
        var mode = GetBatchMode();
        if (mode == BatchPasteQueueMode.Off) return;

        var headBefore = _batchQueue.Head;
        _batchQueue.Enqueue(entry, mode);
        UpdateBatchOrderProperties();
        ReorderAllItemsQueueFirst();
        SyncBatchPasteKeyboardHook();
        if (!fromClipboardMonitor)
            SchedulePushBatchQueueHeadIfChanged(headBefore, mode);
        else if (mode == BatchPasteQueueMode.Fifo)
            _ = TryPushClipboardQueueHeadAsync();
        if (_batchQueue.Count > 0)
            _batchQueueAwaitingNextPasteToSwitchOff = false;
#endif
    }
}
