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
    private async Task<bool> PrepareImageEntryClipboardAsync(
        ClipboardEntry item,
        int clipRetries,
        int clipRetryDelayMs)
    {
        // Codec 会在流关闭前完成解码并 Freeze，同时修复部分 DIB 提供方产生的全零 alpha。
        // SetImage 对 OLE 常延迟读取，不能把仍依赖源流的 BitmapFrame 直接写回剪贴板。
        var imageData = item.TryGetImageData();
        if (imageData is not { Length: > 0 }) return false;
        var swDec = Stopwatch.StartNew();
        var bi = ClipboardImageCodec.DecodePng(imageData);
        swDec.Stop();
        ClipboardDiagnosticsLog.Write(
            $"paste image loadMs={swDec.ElapsedMilliseconds} frame={bi.PixelWidth}x{bi.PixelHeight} storedPng={imageData.Length}");
        var dib = NativeClipboardWriteRetry.CreateDib(bi);
        if (dib != null && await NativeClipboardWriteRetry.TrySetAsync(
                () => Win32.TrySetClipboardDibNative(dib, _hwnd),
                $"SetDib {bi.PixelWidth}x{bi.PixelHeight}",
                _hwnd,
                clipRetries,
                clipRetryDelayMs))
            return true;

        var clipboardOk = await ClipboardWriteRetry.TrySetAsync(
            () => System.Windows.Clipboard.SetImage(bi),
            $"SetImage {bi.PixelWidth}x{bi.PixelHeight}",
            maxRetries: clipRetries,
            delayMs: clipRetryDelayMs,
            clipNudgeHwnd: _hwnd);
        if (!clipboardOk)
            clipboardOk = await PrepareImageEntryFileDropFallbackAsync(item, clipRetries, clipRetryDelayMs);

        return clipboardOk;
    }

    private async Task<bool> PrepareImageEntryFileDropFallbackAsync(
        ClipboardEntry item,
        int clipRetries,
        int clipRetryDelayMs)
    {
        string? tmpPath = null;
        try
        {
            var dir = Path.Combine(Path.GetTempPath(), "ClipboardX");
            Directory.CreateDirectory(dir);
            tmpPath = Path.Combine(dir, $"clip_{DateTime.Now:yyyyMMdd_HHmmss_fff}_fb.png");
            var imageData = item.TryGetImageData();
            if (imageData is not { Length: > 0 }) return false;
            File.WriteAllBytes(tmpPath, ClipboardImageCodec.NormalizePngBytes(imageData));
            var flFb = new StringCollection();
            flFb.Add(tmpPath);
            var clipboardOk = await ClipboardWriteRetry.TrySetAsync(
                () => System.Windows.Clipboard.SetFileDropList(flFb),
                "SetFileDropList imageFallback",
                maxRetries: clipRetries,
                delayMs: clipRetryDelayMs,
                clipNudgeHwnd: _hwnd);
            if (!clipboardOk)
            {
                try { File.Delete(tmpPath); } catch { /* ignore */ }
            }
            else
                ClipboardDiagnosticsLog.Write($"paste image fallback SetFileDropList ok \"{tmpPath}\"");

            return clipboardOk;
        }
        catch (Exception ex)
        {
            ClipboardDiagnosticsLog.Write($"paste image fallback EX {ex.GetType().Name}: {ex.Message}");
            if (tmpPath != null) try { File.Delete(tmpPath); } catch { /* ignore */ }
            return false;
        }
    }
}
