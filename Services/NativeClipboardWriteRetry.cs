using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ClipboardManager;

internal static class NativeClipboardWriteRetry
{
    public static async Task<bool> TrySetAsync(
        Func<bool> trySet,
        string logTag,
        IntPtr owner,
        int maxRetries,
        int baseDelayMs,
        Func<bool>? canContinue = null)
    {
        for (var attempt = 0; attempt < maxRetries; attempt++)
        {
            if (canContinue != null && !canContinue()) return false;
            if (trySet())
            {
                if (attempt > 0)
                    ClipboardDiagnosticsLog.Write($"native clipboard ok retry={attempt} tag={logTag}");
                return true;
            }

            ClipboardDiagnosticsLog.Write(
                $"native clipboard fail attempt={attempt + 1}/{maxRetries} tag={logTag} holder={Win32.DescribeClipboardHolder()}");
            if (attempt + 1 >= maxRetries) break;
            await Task.Delay(Math.Min(baseDelayMs * (1 << Math.Min(attempt, 3)), 400));
        }
        return false;
    }

    public static byte[]? CreateDib(BitmapSource source)
    {
        try
        {
            BitmapSource bitmap = source;
            if (bitmap.Format != PixelFormats.Bgr24)
            {
                var converted = new FormatConvertedBitmap(bitmap, PixelFormats.Bgr24, null, 0);
                converted.Freeze();
                bitmap = converted;
            }

            var width = bitmap.PixelWidth;
            var height = bitmap.PixelHeight;
            if (width <= 0 || height <= 0) return null;
            var stride = (width * 3 + 3) & ~3;
            var pixels = new byte[stride * height];
            bitmap.CopyPixels(pixels, stride, 0);

            const int headerSize = 40;
            var dib = new byte[headerSize + pixels.Length];
            BitConverter.TryWriteBytes(dib.AsSpan(0, 4), headerSize);
            BitConverter.TryWriteBytes(dib.AsSpan(4, 4), width);
            BitConverter.TryWriteBytes(dib.AsSpan(8, 4), height);
            BitConverter.TryWriteBytes(dib.AsSpan(12, 2), (short)1);
            BitConverter.TryWriteBytes(dib.AsSpan(14, 2), (short)24);
            BitConverter.TryWriteBytes(dib.AsSpan(20, 4), pixels.Length);
            for (var y = 0; y < height; y++)
                Buffer.BlockCopy(pixels, y * stride, dib, headerSize + (height - 1 - y) * stride, stride);
            return dib;
        }
        catch (Exception ex)
        {
            ClipboardDiagnosticsLog.Write($"create DIB failed: {ex.GetType().Name}: {ex.Message}");
            return null;
        }
    }
}
