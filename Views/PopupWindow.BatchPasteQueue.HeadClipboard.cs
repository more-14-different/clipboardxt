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
#if CLIPX_CLIPBOARD
    /// <summary>批量队列推剪贴板时：已关模式、队列为空或队首已换则不得再覆盖系统剪贴板（避免 FIFO/LIFO 异步写回盖住用户刚复制的内容）。</summary>
    private bool BatchQueueHeadStillThisEntry(ClipboardEntry item) =>
        _batchQueue.HeadStill(GetBatchMode(), item);

    /// <summary>仅写剪贴板为队首，不发按键；供入队后目标中 Ctrl+V / Shift+Insert 粘贴衔接。</summary>
    private async Task TryPushClipboardQueueHeadAsync()
    {
        await _queueClipboardPushLock.WaitAsync();
        try
        {
            if (GetBatchMode() == BatchPasteQueueMode.Off || _batchQueue.Count == 0) return;
            var item = _batchQueue.Head;
            if (item == null) return;

            _isSettingClipboard = true;
            try
            {
                if (!BatchQueueHeadStillThisEntry(item)) return;

                if (_hwnd != IntPtr.Zero)
                    Win32.TryEmptyClipboardAfterOpen(_hwnd);

                var ok = false;
                const int clipRetries = 8;
                const int clipRetryDelayMs = 55;
                Func<bool> queueCoherence = () => BatchQueueHeadStillThisEntry(item);
                try
                {
                    switch (item.Type)
                    {
                        case EntryType.Text:
                            if (_batchQueueProviderSession != null)
                            {
                                await _batchQueueProviderSession.DisposeAsync();
                                _batchQueueProviderSession = null;
                            }
                            var textPasteSession = CreateAltVTextPasteSession();
                            var textClipboard = await textPasteSession.PrepareClipboardAsync(
                                item.TextContent ?? "",
                                $"queueHead SetText",
                                maxRetries: clipRetries,
                                delayMs: clipRetryDelayMs);
                            _batchQueueProviderSession = textClipboard.ProviderSession;
                            ok = textClipboard.Result.Success;
                            break;
                        case EntryType.Image:
                        {
                            var imageData = item.TryGetImageData();
                            if (imageData is not { Length: > 0 }) break;
                            var bi = ClipboardImageCodec.DecodePng(imageData);
                            ok = await ClipboardWriteRetry.TrySetAsync(
                                () => System.Windows.Clipboard.SetImage(bi),
                                $"queueHead SetImage {bi.PixelWidth}x{bi.PixelHeight}",
                                maxRetries: clipRetries,
                                delayMs: clipRetryDelayMs,
                                clipNudgeHwnd: _hwnd,
                                canContinueBeforeEachAttempt: queueCoherence);
                            if (!ok && BatchQueueHeadStillThisEntry(item))
                            {
                                string? tmpPath = null;
                                try
                                {
                                    var dir = Path.Combine(Path.GetTempPath(), "ClipboardX");
                                    Directory.CreateDirectory(dir);
                                    tmpPath = Path.Combine(dir, $"clip_{DateTime.Now:yyyyMMdd_HHmmss_fff}_fb.png");
                                    File.WriteAllBytes(tmpPath, ClipboardImageCodec.NormalizePngBytes(imageData));
                                    var flFb = new StringCollection();
                                    flFb.Add(tmpPath);
                                    ok = await ClipboardWriteRetry.TrySetAsync(
                                        () => System.Windows.Clipboard.SetFileDropList(flFb),
                                        "queueHead SetFileDropList imageFallback",
                                        maxRetries: clipRetries,
                                        delayMs: clipRetryDelayMs,
                                        clipNudgeHwnd: _hwnd,
                                        canContinueBeforeEachAttempt: queueCoherence);
                                    if (!ok && tmpPath != null) try { File.Delete(tmpPath); } catch { /* ignore */ }
                                }
                                catch (Exception ex)
                                {
                                    ClipboardDiagnosticsLog.Write($"queueHead image fallback EX {ex.GetType().Name}: {ex.Message}");
                                    if (tmpPath != null) try { File.Delete(tmpPath); } catch { /* ignore */ }
                                }
                            }
                            break;
                        }
                        case EntryType.Files:
                        {
                            var fl = new StringCollection();
                            fl.AddRange(item.FilePaths!);
                            ok = await ClipboardWriteRetry.TrySetAsync(
                                () => System.Windows.Clipboard.SetFileDropList(fl),
                                $"queueHead count={fl.Count}",
                                maxRetries: clipRetries,
                                delayMs: clipRetryDelayMs,
                                clipNudgeHwnd: _hwnd,
                                canContinueBeforeEachAttempt: queueCoherence);
                            break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    ClipboardDiagnosticsLog.Write($"queueHead unexpected {ex.GetType().Name}: {ex.Message}");
                }

                ClipboardDiagnosticsLog.Write($"queueHead ok={ok}");
                if (ok && BatchQueueHeadStillThisEntry(item))
                    await Task.Delay(4);
            }
            finally
            {
                _isSettingClipboard = false;
            }
        }
        finally
        {
            _queueClipboardPushLock.Release();
        }
    }
#endif
}
