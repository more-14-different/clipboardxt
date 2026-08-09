using System.ComponentModel;
using System.IO;
using System.Windows;

namespace ClipboardManager;

/// <summary>跳转列表中的唯一路径行；收藏、常用和动态来源是可重叠的元数据。</summary>
public sealed class FileJumpPickerRow : INotifyPropertyChanged
{
    public FileJumpPickerRow(
        string sourceLabel,
        string path,
        bool isFavorite,
        string? phrase = null,
        bool isRecentFolder = false,
        int recentRank = int.MaxValue,
        int favoriteRank = int.MaxValue,
        int contextRank = int.MaxValue)
    {
        SourceLabel = sourceLabel;
        Path = path;
        IsFavorite = isFavorite;
        Phrase = phrase ?? "";
        IsRecentFolder = isRecentFolder;
        RecentRank = recentRank;
        FavoriteRank = favoriteRank;
        ContextRank = contextRank;
    }

    public string SourceLabel { get; }
    public string Path { get; }
    public bool IsFavorite { get; }
    public string Phrase { get; }
    public bool IsRecentFolder { get; }
    public int RecentRank { get; }
    public int FavoriteRank { get; }
    public int ContextRank { get; }

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

    public string IndexLabel => DisplayIndex is >= 1 and <= 9 ? DisplayIndex.ToString() : "";

    public string TypeIcon => "📁";

    public string PreviewLine => PathLineTruncated;

    public string PathLine => Path;

    public string PathLineTruncated => TruncatePathMiddle(Path);

    public string? SubInfo => null;

    public Visibility SubInfoVisibility => string.IsNullOrEmpty(SubInfo) ? Visibility.Collapsed : Visibility.Visible;

    public Visibility StarVisibility => IsFavorite ? Visibility.Visible : Visibility.Collapsed;

    public SearchMetadataChip[] MetadataChips => BuildMetadataChips(null);

    private SearchMetadataChip[]? _displayMetadataChips;
    public SearchMetadataChip[] DisplayMetadataChips
    {
        get => _displayMetadataChips ?? MetadataChips;
        set
        {
            if ((_displayMetadataChips ?? []).SequenceEqual(value ?? [])) return;
            _displayMetadataChips = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisplayMetadataChips)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(MetadataChipsVisibility)));
        }
    }

    internal SearchMetadataChip[] BuildMetadataChips(SearchQuerySpec? spec)
    {
        var chips = new List<SearchMetadataChip>();
        if (IsRecentFolder)
            AddTag(chips, "常用", spec);
        if (!SourceLabel.Equals("收藏", StringComparison.OrdinalIgnoreCase)
            && !SourceLabel.StartsWith("常用", StringComparison.OrdinalIgnoreCase))
            AddTag(chips, SourceLabel, spec);
        if (!string.IsNullOrWhiteSpace(Phrase))
            AddTag(chips, Phrase, spec);
        return chips.ToArray();
    }

    public Visibility MetadataChipsVisibility => DisplayMetadataChips.Length > 0 ? Visibility.Visible : Visibility.Collapsed;

    public string SearchablePrimary
    {
        get
        {
            var source = IsNonSearchableSourceLabel(SourceLabel) ? "" : SourceLabel;
            return $"{Phrase} {source} {Path}";
        }
    }

    private static void AddTag(List<SearchMetadataChip> chips, string? text, SearchQuerySpec? spec)
    {
        text = text?.Trim();
        if (string.IsNullOrWhiteSpace(text)) return;
        if (string.Equals(text, "收藏", StringComparison.OrdinalIgnoreCase)) return;
        if (chips.Any(c => string.Equals(c.Text, text, StringComparison.OrdinalIgnoreCase))) return;
        var isSpecial = IsNonSearchableSourceLabel(text);
        chips.Add(new SearchMetadataChip(text,
            !isSpecial && spec != null && !spec.IsEmpty && TagMatchesQuery(text, spec),
            isSpecial));
    }

    private static bool TagMatchesQuery(string text, SearchQuerySpec spec)
    {
        if (spec.MatchesTextOrPinyin(text, (_, token) => TagTokenMatches(text, token)))
            return true;

        foreach (var token in spec.BroadTokens)
        {
            if (TagTokenMatches(text, token))
                return true;
        }

        return false;
    }

    private static bool TagTokenMatches(string text, string token) =>
        text.Contains(token, StringComparison.OrdinalIgnoreCase)
        || PinyinSearchIndex.MatchesToken(text, token, ClipboardEntry.PinyinFilterMode);

    private static bool IsNonSearchableSourceLabel(string label) =>
        label.StartsWith("常用", StringComparison.OrdinalIgnoreCase)
        || label.Equals("收藏", StringComparison.OrdinalIgnoreCase)
        || label.Equals("everything", StringComparison.OrdinalIgnoreCase);

    private static string TruncatePathMiddle(string path)
    {
        if (string.IsNullOrEmpty(path)) return path;

        const int totalWidth = 50;
        const int tailLen = 18;
        const int headLen = totalWidth - 1 - tailLen;

        if (path.Length <= totalWidth) return path;

        return path[..headLen] + "…" + path[^tailLen..];
    }

    private string? _pinyinCacheKey;
    private string? _pinyinCacheMode;
    private string? _pinyinBlob;

    public string PinyinSearchBlob
    {
        get
        {
            var key = SearchablePrimary;
            var mode = PinyinFilterModes.Normalize(ClipboardEntry.PinyinFilterMode);
            if (_pinyinBlob != null
                && string.Equals(_pinyinCacheKey, key, StringComparison.Ordinal)
                && string.Equals(_pinyinCacheMode, mode, StringComparison.Ordinal))
            {
                return _pinyinBlob;
            }

            _pinyinCacheKey = key;
            _pinyinCacheMode = mode;
            _pinyinBlob = PinyinSearchIndex.BuildBlob(key, mode);
            return _pinyinBlob;
        }
    }

    public bool MatchesSearch(string query)
    {
        var spec = SearchQuerySpec.Parse(query);
        if (spec.IsEmpty) return true;

        return spec.MatchesTextOrPinyin(SearchablePrimary, NormalizeDirectoryAnchorPath(Path), MatchesSearchToken);
    }

    private bool MatchesSearchToken(string searchable, string token)
    {
        if (searchable.Contains(token, StringComparison.OrdinalIgnoreCase)) return true;
        if (PinyinFilterModes.Normalize(ClipboardEntry.PinyinFilterMode) == PinyinFilterModes.Xiaohe)
            return PinyinSearchIndex.MatchesToken(searchable, token, ClipboardEntry.PinyinFilterMode);

        var py = PinyinSearchBlob;
        return py.Length > 0 && py.Contains(token, StringComparison.OrdinalIgnoreCase);
    }

    internal static string NormalizeDirectoryAnchorPath(string path)
    {
        if (string.IsNullOrEmpty(path)) return path;
        var trimmed = path.TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);
        return trimmed.Length == 2 && trimmed[1] == ':' ? trimmed + System.IO.Path.DirectorySeparatorChar : trimmed;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}

internal static class FileJumpPickerRowOrdering
{
    /// <summary>
    /// 全部视图：常用路径严格按 MRU；随后是仅收藏路径；最后是本次上下文候选。
    /// 收藏标签不改变常用路径之间的先后次序。
    /// </summary>
    public static IOrderedEnumerable<FileJumpPickerRow> OrderForDisplay(IEnumerable<FileJumpPickerRow> rows) =>
        rows.OrderByDescending(r => r.IsRecentFolder)
            .ThenBy(r => r.IsRecentFolder ? r.RecentRank : int.MaxValue)
            .ThenByDescending(r => r.IsFavorite)
            .ThenBy(r => r.IsFavorite ? r.FavoriteRank : int.MaxValue)
            .ThenBy(r => r.ContextRank)
            .ThenBy(r => r.Path, StringComparer.OrdinalIgnoreCase);
}
