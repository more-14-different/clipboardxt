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

    /// <summary>
    /// 合法的单条 http/https 文本可由 Alt+Shift+Enter 直接打开。
    /// 该标记由文本实时派生，因此旧记录、新记录和编辑后的记录无需迁移即可保持一致。
    /// </summary>
    public bool IsWebUrl =>
        Type == EntryType.Text && WebUrlLauncher.TryNormalize(TextContent, out _);

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
            var metadata = IsWebUrl ? WebUrlLauncher.MetadataSearchText : "";
            var source = SourceSearchText;
            return string.Join(" ", new[] { SearchableText, metadata, source }
                .Where(part => !string.IsNullOrWhiteSpace(part)));
        }
    }

    /// <summary>
    /// 可检索的各属性值。查询锚点必须在同一个属性值内成立，不能借助拼接文本跨属性命中。
    /// </summary>
    public string[] SearchableValues
    {
        get
        {
            var values = new List<string>();
            AddDistinct(values, ShortcutPhrase);

            switch (Type)
            {
                case EntryType.Text:
                    AddDistinct(values, TextContent);
                    break;
                case EntryType.Files:
                    foreach (var path in FilePaths ?? []) AddDistinct(values, path);
                    break;
                case EntryType.Image:
                    AddDistinct(values, $"image 图片 {ImageWidth}x{ImageHeight}");
                    AddDistinct(values, OcrText);
                    break;
            }

            if (IsWebUrl)
            {
                AddDistinct(values, "网址");
                AddDistinct(values, "URL");
            }

            if (Source != null)
            {
                AddDistinct(values, Source.AppName);
                AddDistinct(values, Source.ExeName);
                AddDistinct(values, Source.ExePath);
                AddDistinct(values, Source.WindowTitle);
                AddDistinct(values, Source.WindowClass);
                AddDistinct(values, Source.FocusedClass);
                if (Source.ProcessId != 0) AddDistinct(values, Source.ProcessId.ToString());
                if (Source.Hwnd != 0) AddDistinct(values, Source.Hwnd.ToString());
                AddDistinct(values, Source.CaptureMethod);
            }

            return values.ToArray();
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

        return MatchesSearch(spec, PinyinFilterMode);
    }

    internal bool MatchesSearch(SearchQuerySpec spec, string? pinyinMode) =>
        spec.MatchesAnyTextOrPinyin(
            SearchableValues,
            (searchable, token) => MatchesSearchToken(searchable, token, pinyinMode));

    private bool MatchesSearchToken(string searchable, string token, string? pinyinMode)
    {
        if (searchable.Contains(token, StringComparison.OrdinalIgnoreCase)) return true;
        // 旧历史的持久化拼音索引早于「网址」派生标签，直接匹配标签可避免要求用户重建索引。
        if (IsWebUrl && WebUrlLauncher.IsMetadataSearchToken(token, pinyinMode)) return true;
        return PinyinSearchIndex.MatchesToken(searchable, token, pinyinMode);
    }

    private static void AddDistinct(List<string> parts, string? value)
    {
        value = value?.Trim();
        if (string.IsNullOrWhiteSpace(value)) return;
        if (parts.Any(x => string.Equals(x, value, StringComparison.OrdinalIgnoreCase))) return;
        parts.Add(value);
    }
}
