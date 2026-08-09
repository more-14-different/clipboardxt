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
    private void OnClipboardUpdate()
    {
        // 仅跳过：不得在 async Set 尚未收尾时清 _isSettingClipboard，否则下一条 WM_CLIPBOARDUPDATE 会当作用户复制 → AutoBatchEnqueue → TryPush 风暴与 CLIPBRD_E 重试卡顿。
        if (_isSettingClipboard)
        {
            ClipboardDiagnosticsLog.Write("monitor skip self_set");
            return;
        }
        // 兜底：_isSettingClipboard 推迟清旗仍可能与本拍 WM 相对错位；序列号 ≤ 自写记录 + 时间窗内 → 仍判定自写回波。
        if (_lastSelfWriteClipboardSeq != 0)
        {
            var nowSeq = Win32.GetClipboardSequenceNumber();
            var dtMs = Environment.TickCount64 - _lastSelfWriteTickMs;
            if (nowSeq == _lastSelfWriteClipboardSeq && dtMs >= 0 && dtMs <= SelfWriteEchoWindowMs)
            {
                ClipboardDiagnosticsLog.Write($"monitor skip self_set_echo seq={nowSeq} dtMs={dtMs}");
                return;
            }
        }
        if (ClipboardGate.IsActive) return;
        if (_pasteInProgress)
        {
            ClipboardDiagnosticsLog.Write("monitor skip pasteInProgress (post-paste echo suppressed)");
            return;
        }

        try
        {
            var source = _sourceTracker.CaptureForClipboardUpdate();

            if (TryReadClipboardBool(() => System.Windows.Clipboard.ContainsFileDropList(), nameof(System.Windows.Clipboard.ContainsFileDropList)))
            {
                var files = TryReadClipboard(() => System.Windows.Clipboard.GetFileDropList(), nameof(System.Windows.Clipboard.GetFileDropList));
                if (files != null && files.Count > 0)
                {
                    var paths = files.Cast<string>().ToArray();
                    ClipboardDiagnosticsLog.Write($"monitor FILES in count={paths.Length} {SummarizeFileDropForLog(paths)}");
                    var fe = new ClipboardEntry { Type = EntryType.Files, FilePaths = paths, Source = source?.Clone() };
                    PreserveDuplicateSourceIfNeeded(fe,
                        x => x.Type == EntryType.Files && string.Join("|", x.FilePaths ?? []) == string.Join("|", paths));
                    DeduplicateFiles(paths);
                    _allItems.Insert(0, fe);
                    TrimItems();
                    _historyStore.TryInsert(fe);
                    AutoBatchEnqueueIfNeeded(fe, fromClipboardMonitor: true);
                    RefreshFilter();
                    return;
                }
            }

            if (TryReadClipboardBool(() => System.Windows.Clipboard.ContainsText(), nameof(System.Windows.Clipboard.ContainsText)))
            {
                var text = TryReadClipboard<string>(() => System.Windows.Clipboard.GetText(), nameof(System.Windows.Clipboard.GetText));
                if (!string.IsNullOrWhiteSpace(text))
                {
                    var te = new ClipboardEntry { Type = EntryType.Text, TextContent = text, Source = source?.Clone() };
                    PreserveDuplicateSourceIfNeeded(te,
                        x => x.Type == EntryType.Text && !x.IsQuickPaste && x.TextContent == text);
                    DeduplicateText(text);
                    _allItems.Insert(0, te);
                    TrimItems();
                    _historyStore.TryInsert(te);
                    AutoBatchEnqueueIfNeeded(te, fromClipboardMonitor: true);
                    RefreshFilter();
                    return;
                }
            }

            if (TryReadClipboardBool(() => System.Windows.Clipboard.ContainsImage(), nameof(System.Windows.Clipboard.ContainsImage)))
            {
                var image = TryReadClipboard<BitmapSource>(() => System.Windows.Clipboard.GetImage(), nameof(System.Windows.Clipboard.GetImage));
                if (image != null)
                {
                    if (image.CanFreeze) image.Freeze();
                    int pw = image.PixelWidth, ph = image.PixelHeight;
                    // 必须在当前 UI 线程、且在剪贴板内容仍有效时立刻编码；丢到 Task.Run 会导致位图跨线程访问或未定义生命期 → 闪退
                    var sw = Stopwatch.StartNew();
                    byte[]? pngData = null;
                    ClipboardDiagnosticsLog.Write($"monitor IMAGE clipboard GetImage {pw}x{ph} → EncodeToPng(sync UI)");
                    try
                    {
                        pngData = ClipboardEntry.EncodeToPng(image);
                    }
                    catch (Exception ex)
                    {
                        ClipboardDiagnosticsLog.Write(
                            $"monitor EncodeToPng EX {pw}x{ph} elapsedMs={sw.ElapsedMilliseconds} {ex.GetType().Name}: {ex.Message}");
                    }
                    sw.Stop();
                    if (pngData == null)
                        ClipboardDiagnosticsLog.Write(
                            $"monitor EncodeToPng returned null {pw}x{ph} elapsedMs={sw.ElapsedMilliseconds}");
                    else
                    {
                        try
                        {
                            ClipboardDiagnosticsLog.Write(
                                $"monitor EncodeToPng OK {pw}x{ph} outBytes={pngData.Length} elapsedMs={sw.ElapsedMilliseconds}");
                            if (pngData.Length > (_appSettings?.MaxImageSizeBytes ?? 15 * 1024 * 1024))
                            {
                                ClipboardDiagnosticsLog.Write($"monitor image skipped: too large bytes={pngData.Length}");
                                return;
                            }
                            var ie = new ClipboardEntry
                            {
                                Type = EntryType.Image, ImageData = pngData,
                                ImageWidth = pw, ImageHeight = ph,
                                Source = source?.Clone()
                            };
                            ie.ImageDataLoader = _historyStore.LoadImageData;
                            var hex = ie.ImageContentMd5Hex;
                            PreserveDuplicateSourceIfNeeded(ie,
                                x => x.Type == EntryType.Image && !x.IsQuickPaste && x.ImageContentMd5Hex == hex);
                            DeduplicateImageByMd5(pngData);
                            _allItems.Insert(0, ie);
                            TrimItems();
                            _historyStore.TryInsert(ie);
                            AutoBatchEnqueueIfNeeded(ie, fromClipboardMonitor: true);
                            if (_appSettings != null)
                                _imageOcrQueue?.Enqueue(ie, _appSettings, OnImageOcrEntryUpdated);
                            RefreshFilter();
                            ClipboardDiagnosticsLog.Write($"monitor history inserted image outBytes={pngData.Length}");
                        }
                        catch (Exception ex)
                        {
                            ClipboardDiagnosticsLog.Write(
                                $"monitor history insert EX {ex.GetType().Name}: {ex.Message}");
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            ClipboardDiagnosticsLog.Write($"monitor outer catch {ex.GetType().Name}: {ex.Message}");
        }
    }
}
