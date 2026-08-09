using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using ClipboardManager.Models;
using ClipboardManager.Services;

namespace ClipboardManager;

public enum EntryType { Text, Image, Files }

public sealed record SearchMetadataChip(string Text, bool IsMatch, bool IsSpecial = false);

public partial class ClipboardEntry : INotifyPropertyChanged
{
    public EntryType Type { get; set; }
    public string? TextContent { get; set; }
    private byte[]? _imageData;
    private bool _imageDataLoaded;

    public byte[]? ImageData
    {
        get => _imageData;
        set
        {
            _imageData = value;
            _imageDataLoaded = value != null;
            _imageMd5Hex = null;
            _thumbnailRef = null;
            _thumbnailUnavailable = false;
            unchecked { _thumbnailLoadGeneration++; }
        }
    }

    [System.Xml.Serialization.XmlIgnore]
    public Func<long, byte[]?>? ImageDataLoader { get; set; }

    public byte[]? TryGetImageData()
    {
        if (_imageData != null) return _imageData;
        if (Type != EntryType.Image || PersistedId is not long id || id <= 0 || _imageDataLoaded)
            return null;
        if (ImageDataLoader == null) return null;
        _imageData = ImageDataLoader(id);
        _imageDataLoaded = true;
        return _imageData;
    }

    public void ReleaseImageData()
    {
        if (PersistedId is long id && id > 0 && ImageDataLoader != null)
        {
            _imageData = null;
            _imageDataLoaded = false;
        }
    }
    public string[]? FilePaths { get; set; }
    public ClipboardSourceInfo? Source { get; set; }

    private byte[]? _sourceIconPng;
    public byte[]? SourceIconPng
    {
        get => _sourceIconPng;
        set
        {
            if (ReferenceEquals(_sourceIconPng, value)) return;
            _sourceIconPng = value;
            _sourceAppIcon = null;
            _sourceAppIconResolved = false;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SourceAppIcon)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasSourceAppIcon)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisplayIcon)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisplayIconVisibility)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TypeFallbackIconVisibility)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ContentTypeBadgeVisibility)));
        }
    }

    private BitmapSource? _sourceAppIcon;
    private bool _sourceAppIconResolved;
    public BitmapSource? SourceAppIcon
    {
        get
        {
            if (_sourceAppIconResolved) return _sourceAppIcon;
            // 面板绑定只解码已持久化图标；不得因旧记录缺图标而访问来源 exe。
            _sourceAppIcon = SourceAppIconCache.DecodeIcon(SourceIconPng);
            _sourceAppIconResolved = true;
            return _sourceAppIcon;
        }
    }

    public DateTime CopiedAt { get; set; } = DateTime.Now;
    public int ImageWidth { get; set; }
    public int ImageHeight { get; set; }

    public string? OcrText { get; set; }

    private bool _isOcrPending;
    public bool IsOcrPending
    {
        get => _isOcrPending;
        set
        {
            if (_isOcrPending == value) return;
            _isOcrPending = value;
            RaiseOcrDisplayPropertiesChanged();
        }
    }

    public void RaiseOcrDisplayPropertiesChanged()
    {
        _pinyinBlob = null;
        _pinyinCacheKey = null;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsOcrPending)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Preview)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SearchableText)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FullSearchableText)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SubInfo)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasSubInfo)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SubInfoVisibility)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ImageMetaLine)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ImageMetaLineVisibility)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(OcrPreviewBody)));
    }

    public bool IsQuickPaste { get; set; }

    private string? _shortcutPhrase;
    public string? ShortcutPhrase
    {
        get => _shortcutPhrase;
        set
        {
            var next = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
            if (_shortcutPhrase == next) return;
            _shortcutPhrase = next;
            _pinyinBlob = null;
            _pinyinCacheKey = null;
            _pinyinCacheMode = null;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShortcutPhrase)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SearchableText)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FullSearchableText)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SubInfo)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SubInfoVisibility)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SearchMetadataPreviewChips)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SearchMetadataPreviewVisibility)));
        }
    }

    private bool _isStarred;
    public bool IsStarred
    {
        get => _isStarred;
        set
        {
            if (_isStarred == value) return;
            _isStarred = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsStarred)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StarVisibility)));
        }
    }

    /// <summary>SQLite 主键；非持久化条目（如尚未入库或快捷短语）为 null。</summary>
    public long? PersistedId { get; set; }

    /// <summary>冷归档来源；普通热表条目为 null。归档项不复用热表主键，避免更新到错误记录。</summary>
    internal int? ArchiveBucketNo { get; set; }
    internal long? ArchiveId { get; set; }
    internal bool IsArchived => ArchiveBucketNo.HasValue && ArchiveId.HasValue;

    /// <summary>粘贴或置顶时更新复制时间并通知列表「时间」列刷新。</summary>
    public void TouchCopiedTime()
    {
        CopiedAt = DateTime.Now;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TimeAgo)));
    }

    /// <summary>就地修改 <see cref="TextContent"/> 后通知列表预览与检索绑定刷新。</summary>
    public void RaiseTextDisplayPropertiesChanged()
    {
        _pinyinBlob = null;
        _pinyinCacheKey = null;
        _pinyinCacheMode = null;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Preview)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SearchableText)));
    }

    private bool _isPendingDelete;
    /// <summary>Del 第一次按下时为 true，表示待二次确认删除（删除线提示）。</summary>
    public bool IsPendingDelete
    {
        get => _isPendingDelete;
        set
        {
            if (_isPendingDelete == value) return;
            _isPendingDelete = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsPendingDelete)));
        }
    }

    private bool _isKeyboardPointFocus;
    /// <summary>键盘/鼠标点选模式下的预选焦点；独立于 SelectedItems。</summary>
    public bool IsKeyboardPointFocus
    {
        get => _isKeyboardPointFocus;
        set
        {
            if (_isKeyboardPointFocus == value) return;
            _isKeyboardPointFocus = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsKeyboardPointFocus)));
        }
    }

    private int _displayIndex;
    public int DisplayIndex
    {
        get => _displayIndex;
        set
        {
            if (_displayIndex == value) return;
            _displayIndex = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisplayIndex)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IndexLabel)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public static int PreviewMaxLines { get; set; } = 2;
    public static string PinyinFilterMode { get; set; } = PinyinFilterModes.Traditional;

    public string IndexLabel => DisplayIndex >= 1 && DisplayIndex <= 9 ? DisplayIndex.ToString() : "";

    private int _batchOrder;
    /// <summary>批量粘贴队列中的顺序（1 起）；0 表示不在队列。</summary>
    public int BatchOrder
    {
        get => _batchOrder;
        set
        {
            if (_batchOrder == value) return;
            _batchOrder = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(BatchOrder)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasBatchOrder)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(BatchOrderLabel)));
        }
    }

    public bool HasBatchOrder => _batchOrder > 0;
    public string BatchOrderLabel => _batchOrder > 0 ? _batchOrder.ToString() : "";

    public string? SubInfo
    {
        get
        {
            if (Type == EntryType.Files && FilePaths is { Length: > 1 }) return $"{FilePaths.Length} 个文件";
            if (Type == EntryType.Image) return ImageMetaLine;
            return null;
        }
    }

    public bool HasSubInfo => SubInfo != null;

    public string TimeAgo
    {
        get
        {
            var span = DateTime.Now - CopiedAt;
            if (span.TotalSeconds < 60) return "刚刚";
            if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes}分钟前";
            if (span.TotalHours < 24) return $"{(int)span.TotalHours}小时前";
            return CopiedAt.ToString("MM-dd HH:mm");
        }
    }

    public bool HasSourceAppIcon => SourceAppIcon != null;
    public bool HasDisplayIcon => DisplayIcon != null;
    public Visibility IconVisibility => HasIcon ? Visibility.Visible : Visibility.Collapsed;
    public Visibility DisplayIconVisibility => HasIcon && HasDisplayIcon ? Visibility.Visible : Visibility.Collapsed;
    public Visibility TypeFallbackIconVisibility => HasIcon && !HasDisplayIcon ? Visibility.Visible : Visibility.Collapsed;
    public Visibility ContentTypeBadgeVisibility => HasIcon && HasDisplayIcon ? Visibility.Visible : Visibility.Collapsed;
    public Visibility ThumbnailVisibility => HasThumbnail ? Visibility.Visible : Visibility.Collapsed;
    public Visibility SubInfoVisibility => HasSubInfo ? Visibility.Visible : Visibility.Collapsed;
    public Visibility StarVisibility => IsStarred ? Visibility.Visible : Visibility.Collapsed;

}
