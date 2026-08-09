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
    /// <summary>多张图片 / 文件 / 图+文混合：图片落盘为 PNG，与文件路径合成同一个 FileDropList，一次 SetClipboard + 一次粘贴。
    /// 这是替代「逐张 SetImage 串行粘贴」的核心稳态路径——目标程序（Word/微信/邮件等）会把 FileDropList 中每个图片当作单独图片插入，
    /// 既消除了段间剪贴板覆盖的时序竞态，又避免了 OLE Image 通告链在多次 SetImage 时累积失败。</summary>
    private async Task RunBatchImagesAndFilesAsFileDropAsync(
        IReadOnlyList<ClipboardEntry> ordered,
        bool hidePopupAfter,
        bool applyHistoryReorder,
        bool ownsGlobalPasteState)
    {
        var paths = new StringCollection();
        var tempPathsToCleanupOnFailure = new List<string>();
        var dir = Path.Combine(Path.GetTempPath(), "ClipboardX");
        try { Directory.CreateDirectory(dir); } catch { }

        foreach (var e in ordered)
        {
            if (e.Type == EntryType.Files && e.FilePaths is { Length: > 0 })
            {
                foreach (var p in e.FilePaths)
                    if (!string.IsNullOrWhiteSpace(p)) paths.Add(p);
            }
            else if (e.Type == EntryType.Image && e.TryGetImageData() is { Length: > 0 } imageData)
            {
                try
                {
                    var p = Path.Combine(dir, $"clip_{DateTime.Now:yyyyMMdd_HHmmss_fff}_{Guid.NewGuid():N}.png");
                    File.WriteAllBytes(p, ClipboardImageCodec.NormalizePngBytes(imageData));
                    paths.Add(p);
                    tempPathsToCleanupOnFailure.Add(p);
                }
                catch (Exception ex)
                {
                    ClipboardDiagnosticsLog.Write($"BATCH_FILEDROP image temp write failed: {ex.GetType().Name}: {ex.Message}");
                }
            }
        }

        if (paths.Count == 0)
        {
            ClipboardDiagnosticsLog.Write("BATCH_FILEDROP no usable paths; skip");
            return;
        }

        if (ownsGlobalPasteState)
            _sequentialPasteHold = true;
        _pasteInProgress = true;
        try
        {
            ClearPendingDelete();
            if (_targetWindow != IntPtr.Zero && !Win32.IsWindow(_targetWindow))
                _targetWindow = IntPtr.Zero;

            ClipboardDiagnosticsLog.Write(
                $"paste BATCH_FILEDROP_ONE_SHOT count={paths.Count} entries={ordered.Count} outerHold={ownsGlobalPasteState}");

            if (hidePopupAfter)
                HidePopup();
            if (_targetWindow != IntPtr.Zero)
                Win32.SetForegroundWindowAggressive(_targetWindow);

            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);

            if (_hwnd != IntPtr.Zero)
                Win32.TryEmptyClipboardAfterOpen(_hwnd);

            _isSettingClipboard = true;
            var ok = await ClipboardWriteRetry.TrySetAsync(
                () => System.Windows.Clipboard.SetFileDropList(paths),
                $"SetFileDropList batchAllAsFiles count={paths.Count}",
                maxRetries: 10,
                delayMs: 50,
                clipNudgeHwnd: _hwnd);
            bool insertedMerged = false;
            if (ok)
            {
                MarkSelfWroteClipboard();
                // 合并产物入库：与文本批量对齐。注意此处的 paths 既包含原始文件路径，也包含图片落盘的临时 PNG 路径。
                if (ordered.Count >= 2)
                {
                    var arr = new string[paths.Count];
                    paths.CopyTo(arr, 0);
                    InsertBatchMergedEntry(new ClipboardEntry { Type = EntryType.Files, FilePaths = arr });
                    insertedMerged = true;
                }
            }
            // 同 RunAllTextBatchSingleClipboardAsync：延迟到 SystemIdle 清旗 + 序列号兜底，避免 WM_CLIPBOARDUPDATE 漏标自写而入历史。
            _ = Dispatcher.BeginInvoke(DispatcherPriority.SystemIdle, () => _isSettingClipboard = false);

            if (ok)
            {
                // FileDrop 写入 → 目标 Open 通常需要 1 帧；用 Background 让一帧而非固定 30ms。
                await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);
                SendPasteToTarget();
            }
            else
            {
                ClipboardDiagnosticsLog.Write("BATCH_FILEDROP SetFileDropList GAVE_UP; cleaning temp PNGs");
                foreach (var p in tempPathsToCleanupOnFailure)
                {
                    try { File.Delete(p); } catch { /* ignore */ }
                }
            }

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
