using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ClipboardManager.Services;

namespace ClipboardManager;

public partial class ClipboardEntry
{
    private string? _imageMd5Hex;
    /// <summary>PNG 图像字节的 MD5（小写十六进制），惰性计算；用于历史项去重。</summary>
    public string? ImageContentMd5Hex
    {
        get
        {
            if (Type != EntryType.Image) return null;
            var data = TryGetImageData();
            if (data is not { Length: > 0 }) return null;
            if (_imageMd5Hex != null) return _imageMd5Hex;
            _imageMd5Hex = ComputeImageBytesMd5Hex(data);
            return _imageMd5Hex;
        }
    }

    public static string ComputeImageBytesMd5Hex(byte[] data)
    {
        if (data == null || data.Length == 0) return "";
        using var md5 = MD5.Create();
        return Convert.ToHexString(md5.ComputeHash(data)).ToLowerInvariant();
    }

    private WeakReference<BitmapSource>? _thumbnailRef;
    private bool _thumbnailUnavailable;
    private int _thumbnailLoadQueued;
    private int _thumbnailLoadGeneration;

    public BitmapSource? Thumbnail
    {
        get
        {
            if (_thumbnailRef != null && _thumbnailRef.TryGetTarget(out var cached))
                return cached;
            if (_thumbnailUnavailable) return null;

            BitmapSource? thumbnail = null;
            if (Type == EntryType.Image)
            {
                if (_imageData == null && ImageDataLoader != null && PersistedId is > 0)
                {
                    QueueLazyThumbnailLoad();
                    return null;
                }

                var wasLazy = _imageData == null;
                var imageData = TryGetImageData();
                if (imageData is { Length: > 0 })
                    thumbnail = CreateThumbnail(imageData);
                if (wasLazy)
                    ReleaseImageData();
            }
            // 文件型图片的原文件可能位于休眠磁盘、失效映射盘或网络路径。
            // 列表渲染只显示通用文件图标；用户打开单项预览时再按需读取原文件。

            if (thumbnail != null)
                _thumbnailRef = new WeakReference<BitmapSource>(thumbnail);
            else
                _thumbnailUnavailable = true;
            return thumbnail;
        }
    }

    private void QueueLazyThumbnailLoad()
    {
        if (_thumbnailUnavailable
            || ImageDataLoader is not { } loader
            || PersistedId is not long id
            || id <= 0
            || Interlocked.CompareExchange(ref _thumbnailLoadQueued, 1, 0) != 0)
            return;

        var generation = _thumbnailLoadGeneration;
        var synchronizationContext = SynchronizationContext.Current;
        _ = Task.Run(() =>
        {
            try
            {
                var bytes = loader(id);
                return bytes is { Length: > 0 } ? CreateThumbnail(bytes) : null;
            }
            catch
            {
                return null;
            }
        }).ContinueWith(task =>
        {
            void Complete()
            {
                Interlocked.Exchange(ref _thumbnailLoadQueued, 0);
                if (generation != _thumbnailLoadGeneration) return;

                var thumbnail = task.Status == TaskStatus.RanToCompletion ? task.Result : null;
                if (thumbnail != null)
                    _thumbnailRef = new WeakReference<BitmapSource>(thumbnail);
                else
                    _thumbnailUnavailable = true;
                NotifyThumbnailChanged();
            }

            if (synchronizationContext != null)
                synchronizationContext.Post(_ => Complete(), null);
            else
                Complete();
        }, TaskScheduler.Default);
    }

    private void NotifyThumbnailChanged()
    {
        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(Thumbnail)));
        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(HasThumbnail)));
        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(HasIcon)));
        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(ThumbnailVisibility)));
        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(IconVisibility)));
        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(DisplayIcon)));
        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(DisplayIconVisibility)));
        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(TypeFallbackIconVisibility)));
        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(ContentTypeBadgeVisibility)));
    }

    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp", ".tiff", ".tif", ".ico"
    };

    public bool IsImageFile => Type == EntryType.Files
        && FilePaths is { Length: >= 1 }
        && ImageExtensions.Contains(Path.GetExtension(FilePaths[0]));

    public int ImageFileCount => Type == EntryType.Files && FilePaths != null
        ? FilePaths.Count(p => ImageExtensions.Contains(Path.GetExtension(p)))
        : 0;

    public bool IsMultiImageFiles => ImageFileCount > 1;

    public string[] GetImageFilePaths() => Type == EntryType.Files && FilePaths != null
        ? FilePaths.Where(p => ImageExtensions.Contains(Path.GetExtension(p))).ToArray()
        : [];

    /// <summary>只有缩略图实际解码成功时才占用缩略图槽；失败时回退到图标/类型标签。</summary>
    public bool HasThumbnail => Thumbnail != null;
    public bool HasIcon => !HasThumbnail;

    private BitmapSource? _fileTypeIcon;
    private string? _fileTypeIconPath;

    /// <summary>
    /// 列表主图标：普通文件优先显示 Shell 文件类型图标，其他类型显示复制来源应用图标。
    /// Shell 取图失败时仍回退到来源应用图标，最终由类型文字兜底。
    /// </summary>
    public BitmapSource? DisplayIcon
    {
        get
        {
            if (Type != EntryType.Files || HasThumbnail)
                return SourceAppIcon;

            var path = FilePaths?.FirstOrDefault();
            if (!string.Equals(_fileTypeIconPath, path, StringComparison.OrdinalIgnoreCase))
            {
                _fileTypeIconPath = path;
                _fileTypeIcon = ShellFileIconCache.GetIconSource(path);
            }

            return _fileTypeIcon ?? SourceAppIcon;
        }
    }

    public string TypeIcon => Type switch
    {
        EntryType.Text => IsQuickPaste ? "QP" : "T",
        EntryType.Image => "IMG",
        EntryType.Files => IsImageFile ? "IMG" : "FILE",
        _ => ""
    };

    public string ContentTypeBadge => TypeIcon;

    public ImageSource TypeFallbackIcon => CommandIconSvg.Get(Type switch
    {
        EntryType.Text when IsQuickPaste => CommandIconKind.QuickPhrase,
        EntryType.Text => CommandIconKind.Text,
        EntryType.Image => CommandIconKind.Image,
        EntryType.Files => CommandIconKind.File,
        _ => CommandIconKind.Clipboard,
    });

    public string Preview => Type switch
    {
        EntryType.Text => TruncateText(TextContent, PreviewMaxLines, 200),
        EntryType.Image => BuildImagePreview(),
        EntryType.Files => FormatFilePaths(),
        _ => ""
    };

    private string BuildImagePreview()
    {
        if (!string.IsNullOrWhiteSpace(OcrText))
            return TruncateText(NormalizeOcrDisplayText(OcrText), PreviewMaxLines, 160);
        if (IsOcrPending) return "识别文字中…";
        return $"{ImageWidth}×{ImageHeight} 图片";
    }

    public string? ImageMetaLine
    {
        get
        {
            if (Type != EntryType.Image) return null;
            var dimensions = $"{ImageWidth}×{ImageHeight}";
            if (IsOcrPending) return $"{dimensions} · 识别中";
            return !string.IsNullOrWhiteSpace(OcrText) ? $"{dimensions} · 图片" : null;
        }
    }

    public bool HasImageMetaLine => ImageMetaLine != null;
    public System.Windows.Visibility ImageMetaLineVisibility => HasImageMetaLine
        ? System.Windows.Visibility.Visible
        : System.Windows.Visibility.Collapsed;

    public string? OcrPreviewBody
    {
        get
        {
            if (Type != EntryType.Image || string.IsNullOrWhiteSpace(OcrText)) return null;
            var text = NormalizeOcrDisplayText(OcrText);
            return text.Length > 4000 ? text[..4000] + "…" : text;
        }
    }

    public bool HasOcrPreviewBody => !string.IsNullOrWhiteSpace(OcrPreviewBody);

    private static string NormalizeOcrDisplayText(string text) =>
        OcrTextPostProcessor.Normalize(text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Trim());

    private static string TruncateText(string? text, int maxLines, int maxChars)
    {
        if (string.IsNullOrEmpty(text)) return "";
        var lines = text.Split('\n');
        var taken = lines.Take(maxLines).Select(l => l.TrimEnd('\r'));
        var result = string.Join("\n", taken);
        if (result.Length > maxChars)
            result = result[..maxChars] + "…";
        else if (lines.Length > maxLines)
            result += " …";
        return result;
    }

    private string FormatFilePaths()
    {
        if (FilePaths == null || FilePaths.Length == 0) return "";
        var names = FilePaths.Select(Path.GetFileName).Take(3);
        var result = string.Join(", ", names);
        if (FilePaths.Length > 3) result += $" (+{FilePaths.Length - 3})";
        return result;
    }

    private static BitmapSource? CreateThumbnail(byte[] imageData)
    {
        try
        {
            return ClipboardImageCodec.DecodePng(imageData, 64);
        }
        catch (Exception ex)
        {
            ClipboardDiagnosticsLog.Write(
                $"thumbnail decode_failed source=clipboard bytes={imageData.Length} " +
                $"error={ex.GetType().Name} hr=0x{ex.HResult:X8}");
            return null;
        }
    }

    public static byte[]? EncodeToPng(BitmapSource image)
        => ClipboardImageCodec.EncodeToPng(image);
}
