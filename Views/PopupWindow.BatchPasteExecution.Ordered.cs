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
    /// <summary>开启合并粘贴且条目类型混合时：仅将相邻的纯文本合并成一段粘贴，图/文件等仍分段粘贴。</summary>
    private async Task RunOrderedPastesWithAdjacentTextMergeAsync(
        IReadOnlyList<ClipboardEntry> items,
        bool newlineAfterEachTextWhenAltEnter)
    {
        var segments = BuildAdjacentRuns(items);
        _sequentialPasteHold = true;
        // 批量入口立刻 Hide：每段自己 hidePopupAfter=isLast 会让前 N-1 段时面板仍前台，
        // SetForegroundWindowAggressive 抢回目标不可靠；先 Hide 让 Win32.SetForegroundWindow 直接成功。
        if (_isPopupVisible && !_popupPinned) HidePopup();
        try
        {
            var opIndex = 0;
            // 任一段是「合并粘贴」（≥2 条文本聚合 / ≥2 条图+文件聚合）时，已通过 InsertBatchMergedEntry 把合并产物置顶；
            // 此时不能再对原选中条目做置顶重排，否则会把它们盖到合并条目之前。
            bool anyMergedSegment = false;
            for (var s = 0; s < segments.Count; s++)
            {
                var seg = segments[s];
                var isLast = s == segments.Count - 1;
                bool segIsImageOrFiles = false;
                if (IsAllTextEntries(seg))
                {
                    if (seg.Count >= 2)
                    {
                        anyMergedSegment = true;
                        await RunAllTextBatchSingleClipboardAsync(
                            seg,
                            newlineAfterEachTextWhenAltEnter,
                            hidePopupAfter: false,
                            applyHistoryReorder: false,
                            ownsGlobalPasteState: false);
                    }
                    else
                    {
                        await PasteEntryAsync(
                            seg[0],
                            hidePopupAfter: false,
                            sequentialSegmentIndex: opIndex,
                            sendNewlineAfterTextWhenAltEnterBatch: newlineAfterEachTextWhenAltEnter);
                    }
                }
                else if (IsAllImageOrFilesEntries(seg))
                {
                    segIsImageOrFiles = true;
                    // 1 张图/1 条文件：保留旧路径（PasteEntryAsync 对单条已是最稳）；
                    // ≥2 条（图+图、文件+文件、图+文件混合）：合并为一次 FileDropList 粘贴。
                    if (seg.Count == 1)
                    {
                        await PasteEntryAsync(
                            seg[0],
                            hidePopupAfter: false,
                            sequentialSegmentIndex: opIndex,
                            sendNewlineAfterTextWhenAltEnterBatch: newlineAfterEachTextWhenAltEnter);
                    }
                    else
                    {
                        anyMergedSegment = true;
                        await RunBatchImagesAndFilesAsFileDropAsync(
                            seg,
                            hidePopupAfter: false,
                            applyHistoryReorder: false,
                            ownsGlobalPasteState: false);
                    }
                }
                else
                {
                    var single = seg[0];
                    segIsImageOrFiles = single.Type == EntryType.Image || single.Type == EntryType.Files;
                    await PasteEntryAsync(
                        single,
                        hidePopupAfter: false,
                        sequentialSegmentIndex: opIndex,
                        sendNewlineAfterTextWhenAltEnterBatch: newlineAfterEachTextWhenAltEnter);
                }
                opIndex++;
                if (!isLast)
                {
                    // 等目标真正读取剪贴板再继续下一段；否则 Word 等慢消费方会出现「漏粘上段、当前段被粘两次」。
                    await WaitForTargetClipboardConsumeAsync(afterImageSegment: segIsImageOrFiles);
                    if (SequentialInterSegmentDelayMs > 0)
                        await Task.Delay(SequentialInterSegmentDelayMs);
                }
            }

            // 简化原则：本轮有「合并粘贴段」时，合并产物已由 InsertBatchMergedEntry 顶到列表第 0 位；
            // 不再对原选中条目做置顶重排，让它们保持原位（用户期望：仅合并字符串顶上去，原条目不动）。
            if (items.Count > 0 && !anyMergedSegment)
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
}
