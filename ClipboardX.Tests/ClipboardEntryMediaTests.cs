using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ClipboardManager.Services;
using System.Diagnostics;
using System.Threading;

namespace ClipboardManager.Tests;

public sealed class ClipboardEntryMediaTests
{
    [Theory]
    [InlineData(CommandIconKind.Clipboard)]
    [InlineData(CommandIconKind.Pin)]
    [InlineData(CommandIconKind.Settings)]
    [InlineData(CommandIconKind.Search)]
    [InlineData(CommandIconKind.Empty)]
    [InlineData(CommandIconKind.Paste)]
    [InlineData(CommandIconKind.OpenUrl)]
    [InlineData(CommandIconKind.Folder)]
    [InlineData(CommandIconKind.Json)]
    [InlineData(CommandIconKind.Edit)]
    [InlineData(CommandIconKind.QuickPhrase)]
    [InlineData(CommandIconKind.Favorite)]
    [InlineData(CommandIconKind.Delete)]
    [InlineData(CommandIconKind.Batch)]
    [InlineData(CommandIconKind.Text)]
    [InlineData(CommandIconKind.Image)]
    [InlineData(CommandIconKind.File)]
    [InlineData(CommandIconKind.Filter)]
    public void CommandSvgIcon_RendersAsFrozenImage(CommandIconKind kind)
    {
        var image = CommandIconSvg.Get(kind);

        Assert.NotNull(image);
        Assert.True(image.IsFrozen);
        Assert.Same(image, CommandIconSvg.Get(kind));
    }

    [Fact]
    public void ValidClipboardImage_UsesThumbnailSlot()
    {
        var entry = new ClipboardEntry
        {
            Type = EntryType.Image,
            ImageData = CreatePng()
        };

        Assert.NotNull(entry.Thumbnail);
        Assert.True(entry.HasThumbnail);
        Assert.False(entry.HasIcon);
        Assert.Equal(Visibility.Visible, entry.ThumbnailVisibility);
        Assert.Equal(Visibility.Collapsed, entry.IconVisibility);
    }

    [Fact]
    public void InvalidClipboardImage_FallsBackToTypeIcon()
    {
        var entry = new ClipboardEntry
        {
            Type = EntryType.Image,
            ImageData = [1, 2, 3, 4]
        };

        Assert.Null(entry.Thumbnail);
        Assert.False(entry.HasThumbnail);
        Assert.True(entry.HasIcon);
        Assert.Equal(Visibility.Collapsed, entry.ThumbnailVisibility);
        Assert.Equal(Visibility.Visible, entry.IconVisibility);
        Assert.Equal(Visibility.Visible, entry.TypeFallbackIconVisibility);
    }

    [Fact]
    public void MissingImageFile_FallsBackToFileIconOrTypeLabel()
    {
        var entry = new ClipboardEntry
        {
            Type = EntryType.Files,
            FilePaths = [Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.png")]
        };

        Assert.True(entry.IsImageFile);
        Assert.Null(entry.Thumbnail);
        Assert.False(entry.HasThumbnail);
        Assert.True(entry.HasIcon);
        Assert.Equal(Visibility.Collapsed, entry.ThumbnailVisibility);
        Assert.Equal(Visibility.Visible, entry.IconVisibility);
        Assert.True(
            entry.DisplayIconVisibility == Visibility.Visible
            || entry.TypeFallbackIconVisibility == Visibility.Visible);
    }

    [Fact]
    public void ExistingImageFile_IsNotReadForListThumbnail()
    {
        var path = Path.Combine(Path.GetTempPath(), $"clipboardx-list-{Guid.NewGuid():N}.png");
        File.WriteAllBytes(path, CreatePng());
        try
        {
            var entry = new ClipboardEntry
            {
                Type = EntryType.Files,
                FilePaths = [path]
            };

            Assert.Null(entry.Thumbnail);
            Assert.False(entry.HasThumbnail);
            Assert.Equal(Visibility.Collapsed, entry.ThumbnailVisibility);
            Assert.Equal(Visibility.Visible, entry.IconVisibility);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Thumbnail_ReevaluatesWhenImageDataIsHydratedLater()
    {
        var entry = new ClipboardEntry { Type = EntryType.Image };

        Assert.False(entry.HasThumbnail);

        entry.ImageData = CreatePng();

        Assert.True(entry.HasThumbnail);
        Assert.NotNull(entry.Thumbnail);
    }

    [Fact]
    public void LazyPersistedImageThumbnail_DoesNotBlockCallingThread()
    {
        using var loaderStarted = new ManualResetEventSlim();
        using var releaseLoader = new ManualResetEventSlim();
        var entry = new ClipboardEntry
        {
            Type = EntryType.Image,
            PersistedId = 42,
            ImageDataLoader = _ =>
            {
                loaderStarted.Set();
                releaseLoader.Wait(TimeSpan.FromSeconds(5));
                return CreatePng();
            }
        };

        var stopwatch = Stopwatch.StartNew();
        var thumbnail = entry.Thumbnail;
        stopwatch.Stop();

        try
        {
            Assert.Null(thumbnail);
            Assert.True(stopwatch.ElapsedMilliseconds < 200, $"elapsed={stopwatch.ElapsedMilliseconds}ms");
            Assert.True(loaderStarted.Wait(TimeSpan.FromSeconds(2)));
        }
        finally
        {
            releaseLoader.Set();
        }
    }

    [Fact]
    public void ZeroAlphaStoredPng_RecoversRgbBeforeThumbnailScaling()
    {
        var entry = new ClipboardEntry
        {
            Type = EntryType.Image,
            ImageData = CreateZeroAlphaPng()
        };

        var thumbnail = Assert.IsAssignableFrom<BitmapSource>(entry.Thumbnail);
        var pixels = CopyBgraPixels(thumbnail);

        Assert.Contains(pixels.Where((_, index) => index % 4 == 3), alpha => alpha == byte.MaxValue);
        Assert.Contains(pixels.Where((_, index) => index % 4 != 3), channel => channel != 0);
    }

    [Fact]
    public void EncodeToPng_RepairsClipboardImageWithRgbBehindZeroAlpha()
    {
        var malformed = CreateZeroAlphaBitmap();

        var png = ClipboardEntry.EncodeToPng(malformed);
        var decoded = ClipboardImageCodec.DecodePng(png!);
        var pixels = CopyBgraPixels(decoded);

        Assert.All(pixels.Where((_, index) => index % 4 == 3), alpha => Assert.Equal(byte.MaxValue, alpha));
        Assert.Contains(pixels.Where((_, index) => index % 4 != 3), channel => channel != 0);
    }

    [Fact]
    public void ShellFileIconCache_NormalizesExtensionKeys()
    {
        Assert.Equal(".pdf", ShellFileIconCache.BuildCacheKey(@"C:\missing\report.PDF"));
        Assert.Equal(".pdf", ShellFileIconCache.BuildCacheKey(@"D:\elsewhere\other.pdf"));
        Assert.Equal("<file>", ShellFileIconCache.BuildCacheKey(@"Z:\offline\extensionless"));
        Assert.Equal("<folder>", ShellFileIconCache.BuildCacheKey(@"Z:\offline\folder\"));
        Assert.Null(ShellFileIconCache.BuildCacheKey(null));
        Assert.Null(ShellFileIconCache.BuildCacheKey("  "));
    }

    [Fact]
    public void SourceAppIconCache_ReusesCachedResult()
    {
        var directory = Path.Combine(Path.GetTempPath(), "ClipboardX.Tests", Guid.NewGuid().ToString("N"));
        var invalidExe = Path.Combine(directory, "invalid.exe");
        Directory.CreateDirectory(directory);
        try
        {
            File.WriteAllBytes(invalidExe, [1, 2, 3, 4]);

            var first = SourceAppIconCache.GetIconSource(invalidExe);
            var second = SourceAppIconCache.GetIconSource(invalidExe);

            Assert.Same(first, second);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void SourceAppIcon_ReevaluatesWhenPersistedPngArrivesLater()
    {
        var entry = new ClipboardEntry { Type = EntryType.Text };

        Assert.Null(entry.SourceAppIcon);

        entry.SourceIconPng = CreatePng();

        Assert.NotNull(entry.SourceAppIcon);
    }

    private static byte[] CreatePng()
    {
        var bitmap = BitmapSource.Create(
            2,
            2,
            96,
            96,
            PixelFormats.Bgra32,
            null,
            new byte[]
            {
                0x20, 0x80, 0xF0, 0xFF,
                0x20, 0x80, 0xF0, 0xFF,
                0x20, 0x80, 0xF0, 0xFF,
                0x20, 0x80, 0xF0, 0xFF
            },
            8);
        return ClipboardEntry.EncodeToPng(bitmap)!;
    }

    private static byte[] CreateZeroAlphaPng()
    {
        var bitmap = CreateZeroAlphaBitmap();
        using var stream = new MemoryStream();
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        encoder.Save(stream);
        return stream.ToArray();
    }

    private static BitmapSource CreateZeroAlphaBitmap() => BitmapSource.Create(
        2,
        2,
        96,
        96,
        PixelFormats.Bgra32,
        null,
        new byte[]
        {
            0x20, 0x80, 0xF0, 0x00,
            0x30, 0x70, 0xE0, 0x00,
            0x40, 0x60, 0xD0, 0x00,
            0x50, 0x50, 0xC0, 0x00
        },
        8);

    private static byte[] CopyBgraPixels(BitmapSource source)
    {
        Assert.Equal(PixelFormats.Bgra32, source.Format);
        var stride = source.PixelWidth * 4;
        var pixels = new byte[stride * source.PixelHeight];
        source.CopyPixels(pixels, stride, 0);
        return pixels;
    }
}
