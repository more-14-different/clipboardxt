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
    /// <param name="sequentialSegmentIndex">≥0 表示连续粘贴中的第几段（无段间延时）；-1 表示单次粘贴（保留对焦/剪贴板/回波延时）。</param>
    /// <param name="sendNewlineAfterTextWhenAltEnterBatch">多选且由 Alt+Enter 触发时：每一小段为文本则写入剪贴板时在末尾追加系统换行（不再发键盘回车）。</param>
    private async Task PasteEntryAsync(
        ClipboardEntry item,
        bool hidePopupAfter,
        int sequentialSegmentIndex = -1,
        bool sendNewlineAfterTextWhenAltEnterBatch = false)
    {
        hidePopupAfter = hidePopupAfter && !_popupPinned;
        _pasteInProgress = true;
        // 单次粘贴成功后会改为 true，让 finally 跳过同步清标志，由后台定时器在回波窗口结束后清。
        bool deferPasteFlagToTimer = false;
        bool clipboardOk = false;
        bool usedNonClipboardTextInsert = false;
        AltVClipboardProvider.Session? providerSession = null;
        bool noSegmentDelays = sequentialSegmentIndex >= 0;
        var textPasteSession = CreateAltVTextPasteSession();
        try
        {
        ClearPendingDelete();

        // 连续粘贴时若每段都置顶+写库+通知 UI，列表会逐条「蹦」且加重卡顿；整轮结束后再统一排序（见 ApplyDeferredSequentialPasteHistoryOrder）。
        if (!item.IsQuickPaste && !_sequentialPasteHold)
        {
            if (item.IsArchived)
                _historyStore.TryRestoreArchived(item);
            var idx = _allItems.IndexOf(item);
            if (idx > 0) { _allItems.RemoveAt(idx); _allItems.Insert(0, item); }
            item.TouchCopiedTime();
            if (item.PersistedId is long pid)
                _historyStore.TryUpdateCopiedAt(pid, item.CopiedAt);
        }

        if (await TryPasteEntryIntoFileJumpSearchAsync(item, hidePopupAfter))
            return;

        if (_targetWindow != IntPtr.Zero && !Win32.IsWindow(_targetWindow))
            _targetWindow = IntPtr.Zero;

        if (hidePopupAfter
            && sequentialSegmentIndex < 0
            && item.Type == EntryType.Text
            && await TryDeliverTextToExternalSinkAsync(item))
        {
            ClipboardDiagnosticsLog.Write(
                $"paste success path=externalTextSink len={item.TextContent?.Length ?? 0}");
            HidePopup();
            return;
        }

        var tgt = _targetWindow.ToInt64();
        ClipboardDiagnosticsLog.Write(item.Type switch
        {
            EntryType.Text => $"paste BEGIN Text len={item.TextContent?.Length ?? 0} target=0x{tgt:X} gwFocus={Win32.GetForegroundWindow().ToInt64():X}",
            EntryType.Image => $"paste BEGIN Image pngBytes={item.TryGetImageData()?.Length ?? 0} target=0x{tgt:X}",
            EntryType.Files => $"paste BEGIN Files {SummarizeFileDropForLog(item.FilePaths ?? [])} target=0x{tgt:X}",
            _ => $"paste BEGIN type={item.Type} target=0x{tgt:X}"
        });

        // Alt+V 面板本身是 no-activate；先 Hide 再写剪贴板，尽量保持原输入上下文不变。
        if (hidePopupAfter)
            HidePopup();
        var textLen = item.TextContent?.Length ?? 0;
        var imgBytes = item.Type == EntryType.Image ? item.TryGetImageData()?.Length ?? 0 : 0;
        var imgPixels = item.Type == EntryType.Image ? item.ImageWidth * item.ImageHeight : 0;
        var hugeClipboardImage = item.Type == EntryType.Image && (imgBytes > 900_000 || imgPixels > 1_200_000);
        if (!noSegmentDelays)
        {
            // 焦点切回后等待目标进程消息泵处理一轮；以前的较大值是为兼容个别拖慢的 OLE 富文本场景，
            // 实测 99% 的小文本场景下根本无需等待，零延时即可成功；只在大文本/图片上保留延长。
            // 早期还多做了一次 Dispatcher.InvokeAsync(Background) 让步，但 await Task.Delay 本身已经让步过，
            // 再额外排队一次到 UI 队列只会让点击响应更晚一帧（~16ms 起），故移除。
            var focusSettleMs = item.Type switch
            {
                EntryType.Text => textLen > 12000 ? 100 : textLen > 4000 ? 50 : 0,
                EntryType.Image => hugeClipboardImage ? 160 : 90,
                _ => 15
            };
            if (focusSettleMs > 0)
                await Task.Delay(focusSettleMs);
            ClipboardDiagnosticsLog.Write($"paste focusSettleMs={focusSettleMs} after HidePopup");
        }
        else
        {
            if (sequentialSegmentIndex == 0)
                await Dispatcher.Yield();
            ClipboardDiagnosticsLog.Write($"paste sequential segment {sequentialSegmentIndex} (no focus settle delay)");
        }

        if (_hwnd != IntPtr.Zero && item.Type != EntryType.Text)
        {
            var nudged = Win32.TryEmptyClipboardAfterOpen(_hwnd);
            ClipboardDiagnosticsLog.Write($"paste clipNudge EmptyClipboard ok={nudged}");
        }

        _isSettingClipboard = true;
        var clipRetries = noSegmentDelays ? 8 : 2;
        var clipRetryDelayMs = noSegmentDelays ? 60 : 40;
        try
        {
            switch (item.Type)
            {
                case EntryType.Text:
                    var textResult = await PrepareTextEntryClipboardAsync(
                        item,
                        textPasteSession,
                        sendNewlineAfterTextWhenAltEnterBatch,
                        noSegmentDelays,
                        clipRetries,
                        clipRetryDelayMs);
                    clipboardOk = textResult.ClipboardOk;
                    usedNonClipboardTextInsert = textResult.UsedNonClipboardTextInsert;
                    providerSession = textResult.ProviderSession;
                    break;
                case EntryType.Image:
                    clipboardOk = await PrepareImageEntryClipboardAsync(item, clipRetries, clipRetryDelayMs);
                    break;
                case EntryType.Files:
                    clipboardOk = await PrepareFilesEntryClipboardAsync(item, clipRetries, clipRetryDelayMs);
                    break;
            }
        }
        catch (Exception ex)
        {
            ClipboardDiagnosticsLog.Write($"paste unexpected before/during set {ex.GetType().Name}: {ex.Message}");
        }

        ClipboardDiagnosticsLog.Write(
            $"paste END clipboardOk={clipboardOk} directText={usedNonClipboardTextInsert} willSendPaste={clipboardOk && !usedNonClipboardTextInsert}");

        if (!clipboardOk)
            _isSettingClipboard = false;
        else if (usedNonClipboardTextInsert)
            _isSettingClipboard = false;
        else if (noSegmentDelays)
        {
            // 连续段：短让步后同步清标志，避免误标「外部复制」。
            await Task.Delay(8);
            _isSettingClipboard = false;
        }
        else
            _ = Dispatcher.BeginInvoke(DispatcherPriority.SystemIdle, () => _isSettingClipboard = false);

        if (clipboardOk && !usedNonClipboardTextInsert)
        {
            if (!noSegmentDelays)
            {
                // 写完剪贴板到发送 Ctrl+V 之间，只有 OLE 图像需要等一拍让 IDataObject 真正落盘；
                // 文本/文件路径下立即 SendInput 即可，避免「按下到看见粘贴出来」之间多出一帧延迟。
                var prePasteDelayMs = item.Type == EntryType.Image ? 50 : 0;
                if (prePasteDelayMs > 0)
                    await Task.Delay(prePasteDelayMs);
            }

            var pasteDispatch = textPasteSession.DispatchPaste();
            textPasteSession.LogSuccessPath("paste", "clipboardProvider", pasteDispatch);

            // 连续粘贴：整轮结束后再 TailSettle；段间另有 SequentialInterSegmentDelayMs。
            // 单次粘贴：以前同步 await 600ms 回波窗口，导致「按完按钮要等一下」的卡顿；
            // 改为后台定时器在 _pasteInProgress 上挡回波，方法立即返回不阻塞调用方。
            if (!noSegmentDelays)
            {
                const int postEchoMs = 600;
                deferPasteFlagToTimer = true;
                _ = Task.Delay(postEchoMs).ContinueWith(_ =>
                {
                    if (!_sequentialPasteHold) _pasteInProgress = false;
                    ClipboardDiagnosticsLog.Write($"paste post-echo suppression window elapsed (ms={postEchoMs})");
                }, TaskScheduler.Default);
            }
        }
        else if (usedNonClipboardTextInsert)
        {
            ClipboardDiagnosticsLog.Write("paste success path=directText mode=nonClipboard");
            deferPasteFlagToTimer = false;
        }
        }
        finally
        {
            if (providerSession != null)
            {
                if (clipboardOk && !usedNonClipboardTextInsert)
                    await textPasteSession.AwaitProviderSettleAsync(noSegmentDelays);
                await providerSession.DisposeAsync();
            }
            if (!_sequentialPasteHold && !deferPasteFlagToTimer)
                _pasteInProgress = false;
        }
    }
}
