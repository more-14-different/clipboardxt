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
    /// <summary>纯文本多选：拼成一段一次 SetClipboard + 一次粘贴，等价于逐条贴接在一起，但无 N 次剪贴板轮询与段间等待。</summary>
    private async Task RunAllTextBatchSingleClipboardAsync(
        IReadOnlyList<ClipboardEntry> ordered,
        bool newlineAfterEachTextWhenAltEnter,
        bool hidePopupAfter = true,
        bool applyHistoryReorder = true,
        bool ownsGlobalPasteState = true)
    {
        int cap = 0;
        var nlLen = Environment.NewLine.Length;
        foreach (var e in ordered)
        {
            cap += (e.TextContent?.Length ?? 0);
            if (newlineAfterEachTextWhenAltEnter)
                cap += nlLen;
        }
        var sb = new StringBuilder(cap + 8);
        foreach (var e in ordered)
        {
            sb.Append(e.TextContent);
            if (newlineAfterEachTextWhenAltEnter)
                sb.Append(Environment.NewLine);
        }
        var combined = sb.ToString();

        if (ownsGlobalPasteState)
            _sequentialPasteHold = true;
        _pasteInProgress = true;
        try
        {
            ClearPendingDelete();
            if (_targetWindow != IntPtr.Zero && !Win32.IsWindow(_targetWindow))
                _targetWindow = IntPtr.Zero;

            ClipboardDiagnosticsLog.Write(
                $"paste BATCH_TEXT_ONE_SHOT count={ordered.Count} len={combined.Length} altNl={newlineAfterEachTextWhenAltEnter} outerHold={ownsGlobalPasteState}");

            if (hidePopupAfter)
                HidePopup();

            // 让出一帧给前台切换，比固定 26ms 更短且更稳定。
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);

            if (_hwnd != IntPtr.Zero)
                Win32.TryEmptyClipboardAfterOpen(_hwnd);

            _isSettingClipboard = true;
            var textPasteSession = CreateAltVTextPasteSession();
            var textClipboard = await textPasteSession.PrepareClipboardAsync(
                combined,
                "paste batchText",
                maxRetries: 10,
                delayMs: 45);
            await using var providerSession = textClipboard.ProviderSession;
            var clipboardResult = textClipboard.Result;
            var ok = clipboardResult.Success;
            var usedNonClipboardBatchInsert = false;
            bool insertedMerged = false;
            if (ok)
            {
                MarkSelfWroteClipboard();
                // 合并产物入库：与系统剪贴板状态对齐，便于用户后续复用整段。回波拦截（旗 + 序列号）确保 OnClipboardUpdate 不会重复入库。
                if (ordered.Count >= 2)
                {
                    InsertBatchMergedEntry(new ClipboardEntry { Type = EntryType.Text, TextContent = combined });
                    insertedMerged = true;
                }
            }
            else
            {
                ok = textPasteSession.TryInsertWithoutClipboard(combined, "paste batchText", out usedNonClipboardBatchInsert);
            }
            // 不立刻清 _isSettingClipboard：必须等本次 SetText 触发的 WM_CLIPBOARDUPDATE 派发完，
            // 否则该消息到达时旗已落，OnClipboardUpdate 会把合并文本当成「用户复制」入库。
            // 用 SystemIdle 排队保证本拍消息泵已处理完后再清；序列号 + 时间窗作为兜底。
            _ = Dispatcher.BeginInvoke(DispatcherPriority.SystemIdle, () => _isSettingClipboard = false);

            if (ok)
            {
                if (!usedNonClipboardBatchInsert)
                {
                    var pasteDispatch = textPasteSession.DispatchPaste();
                    textPasteSession.LogSuccessPath("paste batchText", "clipboardProvider", pasteDispatch);
                    if (providerSession != null)
                        await textPasteSession.AwaitProviderSettleAsync(noSegmentDelays: false);
                }
                else
                {
                    ClipboardDiagnosticsLog.Write("paste batchText success path=directText mode=nonClipboard");
                }
            }

            // 已插入合并条目时不再对原条目重排：用户期望「仅合并字符串顶上去，原条目不动」。
            if (applyHistoryReorder && !insertedMerged)
                ApplyDeferredSequentialPasteHistoryOrder(ordered);
            await Task.Delay(20);
        }
        finally
        {
            if (ownsGlobalPasteState)
            {
                _sequentialPasteHold = false;
                _pasteInProgress = false;
            }
        }
    }
}
