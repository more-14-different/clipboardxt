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
    /// <summary>连续段之间最小让步：仅作为下限，真正的等待由 <see cref="WaitForTargetClipboardConsumeAsync"/> 决定。
    /// 早期硬编码 22ms 在 Word 接收图片 OLE（常需 100~300ms）场景下会出现「漏粘上一段、当前段被粘两次」的伪重复。</summary>
    private const int SequentialInterSegmentDelayMs = 22;

    /// <summary>段间等待目标程序消费（OpenClipboard/读取）剪贴板的最长时长；超时则按下一段直接覆盖处理。</summary>
    private const int SequentialInterSegmentMaxWaitMs = 350;

    /// <summary>图片段写入剪贴板后，目标程序对 OLE 通告链处理常显著慢于文本，给出更宽松的等待上限。</summary>
    private const int SequentialInterSegmentMaxWaitMsForImage = 600;

    /// <summary>整轮结束后稍晚再解除「粘贴中」。</summary>
    private const int SequentialTailSettleMs = 85;

    /// <summary>
    /// 段间等待：以剪贴板序列号 + 「目标 OpenClipboard 持有过的瞬间」为信号，确认目标程序已消化上一段；
    /// 超时则放弃等待按下一段继续。返回实际等待毫秒数。
    /// </summary>
    /// <param name="afterImageSegment">为 true 时使用图片专用的更长上限。</param>
    private async Task<int> WaitForTargetClipboardConsumeAsync(bool afterImageSegment)
    {
        var maxMs = afterImageSegment ? SequentialInterSegmentMaxWaitMsForImage : SequentialInterSegmentMaxWaitMs;
        var startSeq = Win32.GetClipboardSequenceNumber();
        var sw = Stopwatch.StartNew();
        var lastOwner = IntPtr.Zero;
        bool sawForeignOpen = false;
        // 轮询步长 12ms：和我们 Set 后到目标 Open 的典型时延量级匹配，且 30 次内即可铺满 350ms 上限。
        while (sw.ElapsedMilliseconds < maxMs)
        {
            await Task.Delay(12);
            // 序列号变化意味着剪贴板已被「另一次写入」覆盖（不期望发生，跳出立即返回让外层日志记录）。
            if (Win32.GetClipboardSequenceNumber() != startSeq)
            {
                ClipboardDiagnosticsLog.Write($"interSeg wait: seq changed earlier than expected ({sw.ElapsedMilliseconds}ms)");
                break;
            }
            var owner = Win32.GetOpenClipboardWindow();
            if (owner != IntPtr.Zero && owner != _hwnd && owner != lastOwner)
            {
                lastOwner = owner;
                sawForeignOpen = true;
                // 目标已开始读取——再让出极短时间给 IDataObject GetData 完成
                await Task.Delay(20);
                break;
            }
        }
        sw.Stop();
        ClipboardDiagnosticsLog.Write(
            $"interSeg waitMs={sw.ElapsedMilliseconds} max={maxMs} sawForeignOpen={sawForeignOpen} afterImage={afterImageSegment}");
        return (int)sw.ElapsedMilliseconds;
    }

    /// <summary>多段粘贴共享外部「粘贴进行中」标志，避免每段之间剪贴板监听插队。</summary>
    private async Task RunSequentialPastesAsync(
        IReadOnlyList<ClipboardEntry> items,
        bool newlineAfterEachTextWhenAltEnter = false,
        bool softLineBreakAfterEachTextWhenShiftEnter = false)
    {
        _sequentialPasteHold = true;
        if (_isPopupVisible && !_popupPinned) HidePopup();
        try
        {
            for (int i = 0; i < items.Count; i++)
            {
                var cur = items[i];
                await PasteEntryAsync(
                    cur,
                    hidePopupAfter: false,
                    sequentialSegmentIndex: i,
                    sendNewlineAfterTextWhenAltEnterBatch: newlineAfterEachTextWhenAltEnter);
                if (softLineBreakAfterEachTextWhenShiftEnter && cur.Type == EntryType.Text)
                {
                    await WaitForTargetClipboardConsumeAsync(afterImageSegment: false);
                    SendSoftLineBreakToTarget();
                    await Task.Delay(SequentialInterSegmentDelayMs);
                }
                if (i < items.Count - 1)
                {
                    if (!softLineBreakAfterEachTextWhenShiftEnter || cur.Type != EntryType.Text)
                    {
                        await WaitForTargetClipboardConsumeAsync(
                            afterImageSegment: cur.Type == EntryType.Image || cur.Type == EntryType.Files);
                    }
                    if (SequentialInterSegmentDelayMs > 0)
                        await Task.Delay(SequentialInterSegmentDelayMs);
                }
            }

            if (items.Count > 0)
                ApplyDeferredSequentialPasteHistoryOrder(items);

            if (items.Count > 0)
                await Task.Delay(SequentialTailSettleMs);
        }
        finally
        {
            _sequentialPasteHold = false;
            _pasteInProgress = false;
        }
    }

    /// <summary>连续粘贴结束后，按「后贴的在最上」依次插队置顶并补写库，只做一次列表刷新。</summary>
    private void ApplyDeferredSequentialPasteHistoryOrder(IReadOnlyList<ClipboardEntry> itemsInPasteOrder)
    {
        if (itemsInPasteOrder.Count == 0) return;
        for (int j = itemsInPasteOrder.Count - 1; j >= 0; j--)
        {
            var item = itemsInPasteOrder[j];
            if (item.IsQuickPaste) continue;
            var idx = _allItems.IndexOf(item);
            if (idx > 0) { _allItems.RemoveAt(idx); _allItems.Insert(0, item); }
            item.TouchCopiedTime();
            if (item.PersistedId is long pid)
                _historyStore.TryUpdateCopiedAt(pid, item.CopiedAt);
        }
        RefreshFilter(0);
    }
}
