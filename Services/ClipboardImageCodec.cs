using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ClipboardManager.Services;

/// <summary>
/// Windows 剪贴板的部分 DIB 提供方会保留 RGB，却把所有 alpha 写成 0。WPF 按规范将其视为
/// 全透明，并且在缩放时会因预乘 alpha 丢掉 RGB。本类在任何缩放之前识别这种载荷并恢复为不透明图。
/// </summary>
internal static class ClipboardImageCodec
{
    public static BitmapSource DecodePng(byte[] png, int decodePixelWidth = 0)
    {
        var preview = DecodeCore(png, decodePixelWidth);
        if (!HasAllZeroAlpha(preview)) return preview;

        // DecodePixelWidth 会在 alpha=0 时把 RGB 一并预乘成 0；必须重新解码原尺寸才能恢复旧记录。
        var full = decodePixelWidth > 0 ? DecodeCore(png, 0) : preview;
        var normalized = MakeOpaqueWhenRgbExistsBehindZeroAlpha(full, out var repaired);
        if (!repaired) return preview;

        ClipboardDiagnosticsLog.Write(
            $"image alpha_repaired source=stored_png frame={full.PixelWidth}x{full.PixelHeight}");
        return ResizeToWidth(normalized, decodePixelWidth);
    }

    public static byte[] NormalizePngBytes(byte[] png)
    {
        try
        {
            var source = DecodeCore(png, 0);
            var normalized = MakeOpaqueWhenRgbExistsBehindZeroAlpha(source, out var repaired);
            return repaired ? EncodeCore(normalized) : png;
        }
        catch
        {
            return png;
        }
    }

    public static byte[]? EncodeToPng(BitmapSource image)
    {
        try
        {
            var normalized = MakeOpaqueWhenRgbExistsBehindZeroAlpha(image, out var repaired);
            if (repaired)
            {
                ClipboardDiagnosticsLog.Write(
                    $"image alpha_repaired source=clipboard frame={image.PixelWidth}x{image.PixelHeight}");
            }
            return EncodeCore(normalized);
        }
        catch
        {
            return null;
        }
    }

    internal static BitmapSource MakeOpaqueWhenRgbExistsBehindZeroAlpha(
        BitmapSource source,
        out bool repaired)
    {
        repaired = false;
        if (source.Format != PixelFormats.Bgra32) return source;

        var stride = checked(source.PixelWidth * 4);
        var pixels = new byte[checked(stride * source.PixelHeight)];
        source.CopyPixels(pixels, stride, 0);

        var hasRgb = false;
        for (var i = 0; i < pixels.Length; i += 4)
        {
            if (pixels[i + 3] != 0) return source;
            hasRgb |= pixels[i] != 0 || pixels[i + 1] != 0 || pixels[i + 2] != 0;
        }
        if (!hasRgb) return source;

        for (var i = 3; i < pixels.Length; i += 4)
            pixels[i] = byte.MaxValue;

        var normalized = BitmapSource.Create(
            source.PixelWidth,
            source.PixelHeight,
            source.DpiX > 0 ? source.DpiX : 96,
            source.DpiY > 0 ? source.DpiY : 96,
            PixelFormats.Bgra32,
            null,
            pixels,
            stride);
        normalized.Freeze();
        repaired = true;
        return normalized;
    }

    private static BitmapSource DecodeCore(byte[] png, int decodePixelWidth)
    {
        using var stream = new MemoryStream(png);
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.StreamSource = stream;
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        if (decodePixelWidth > 0)
            bitmap.DecodePixelWidth = decodePixelWidth;
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }

    private static bool HasAllZeroAlpha(BitmapSource source)
    {
        if (source.Format != PixelFormats.Bgra32 && source.Format != PixelFormats.Pbgra32)
            return false;

        var stride = checked(source.PixelWidth * 4);
        var pixels = new byte[checked(stride * source.PixelHeight)];
        source.CopyPixels(pixels, stride, 0);
        for (var i = 3; i < pixels.Length; i += 4)
        {
            if (pixels[i] != 0) return false;
        }
        return true;
    }

    private static BitmapSource ResizeToWidth(BitmapSource source, int targetWidth)
    {
        if (targetWidth <= 0 || source.PixelWidth <= targetWidth) return source;

        var scale = (double)targetWidth / source.PixelWidth;
        var resized = new TransformedBitmap(source, new ScaleTransform(scale, scale));
        resized.Freeze();
        return resized;
    }

    private static byte[] EncodeCore(BitmapSource image)
    {
        using var stream = new MemoryStream();
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(image));
        encoder.Save(stream);
        return stream.ToArray();
    }
}
