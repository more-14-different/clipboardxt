using System.ComponentModel;
using System.IO;
using System.Windows;
using ClipboardManager.Services;

namespace ClipboardManager;

public partial class ClipboardEntry
{
    private SearchMetadataChip[]? _searchMetadataPreviewChips;
    public SearchMetadataChip[]? SearchMetadataPreviewChips
    {
        get => _searchMetadataPreviewChips;
        set
        {
            if ((_searchMetadataPreviewChips ?? []).SequenceEqual(value ?? [])) return;
            _searchMetadataPreviewChips = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SearchMetadataPreviewChips)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SearchMetadataPreviewVisibility)));
        }
    }

    public Visibility SearchMetadataPreviewVisibility =>
        SearchMetadataPreviewChips is { Length: > 0 } ? Visibility.Visible : Visibility.Collapsed;

    public SearchMetadataChip? ShortcutPhraseMetadataChip =>
        string.IsNullOrWhiteSpace(ShortcutPhrase) ? null : new SearchMetadataChip(ShortcutPhrase, false);

    public string[] SourceMetadataDisplayParts
    {
        get
        {
            if (Source == null || !Source.HasAny) return [];
            var parts = new List<string>();
            AddDistinct(parts, Source.DisplayName);
            AddDistinct(parts, Source.WindowTitle);
            AddDistinct(parts, Source.ExeName);
            AddDistinct(parts, Source.WindowClass);
            AddDistinct(parts, Source.FocusedClass);
            AddDistinct(parts, Source.ExePath);
            return parts.ToArray();
        }
    }

    public string SearchableText
    {
        get
        {
            var baseText = Type switch
            {
                EntryType.Text => TextContent ?? "",
                EntryType.Files => string.Join(" ", FilePaths ?? []),
                EntryType.Image => BuildImageSearchableText(),
                _ => ""
            };
            return ShortcutPhrase != null ? $"{ShortcutPhrase} {baseText}" : baseText;
        }
    }

    private string BuildImageSearchableText()
    {
        var dimensions = $"image 图片 {ImageWidth}x{ImageHeight}";
        return string.IsNullOrWhiteSpace(OcrText) ? dimensions : $"{dimensions} {OcrText}";
    }

    public string SourceSearchText => Source?.BuildSearchText() ?? "";

    public string FullSearchableText
    {
        get
        {
            var source = SourceSearchText;
            return string.IsNullOrWhiteSpace(source) ? SearchableText : $"{SearchableText} {source}";
        }
    }

    /// <summary>
    /// 当前中文过滤模式对应的拼音检索串（小写、无空格），用于 QuickPaste 内存检索。
    /// 普通历史的拼音检索已下沉到 SQLite FTS5。
    /// </summary>
    private string? _pinyinCacheKey;
    private string? _pinyinCacheMode;
    private string? _pinyinBlob;

    public string PinyinSearchBlob
    {
        get
        {
            var key = FullSearchableText;
            var mode = PinyinFilterModes.Normalize(PinyinFilterMode);
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

    internal void SetPersistedPinyinSearchBlob(string? value, string? mode)
    {
        _pinyinCacheKey = FullSearchableText;
        _pinyinCacheMode = PinyinFilterModes.Normalize(mode);
        _pinyinBlob = value ?? "";
    }

    public bool MatchesSearch(string query)
    {
        var spec = SearchQuerySpec.Parse(query);
        if (spec.IsEmpty) return true;

        return spec.MatchesTextOrPinyin(FullSearchableText, SearchableText, MatchesSearchToken);
    }

    private bool MatchesSearchToken(string searchable, string token)
    {
        if (searchable.Contains(token, StringComparison.OrdinalIgnoreCase)) return true;
        if (PinyinFilterModes.Normalize(PinyinFilterMode) == PinyinFilterModes.Traditional)
        {
            var py = PinyinSearchBlob;
            return py.Length > 0 && py.Contains(token, StringComparison.OrdinalIgnoreCase);
        }

        return PinyinSearchIndex.MatchesToken(searchable, token, PinyinFilterMode);
    }

    private static void AddDistinct(List<string> parts, string? value)
    {
        value = value?.Trim();
        if (string.IsNullOrWhiteSpace(value)) return;
        if (parts.Any(x => string.Equals(x, value, StringComparison.OrdinalIgnoreCase))) return;
        parts.Add(value);
    }
}
