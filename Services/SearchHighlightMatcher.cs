using System.Collections.Concurrent;
using System.Globalization;
using System.Text.RegularExpressions;

namespace ClipboardManager;

internal static class SearchHighlightMatcher
{
    private const int CacheCapacity = 512;
    private static readonly ConcurrentDictionary<CacheKey, (int Start, int End)[]> RangeCache = new();
    private static readonly ConcurrentQueue<CacheKey> CacheOrder = new();
    private static readonly CompareInfo Compare = CultureInfo.InvariantCulture.CompareInfo;

    /// <summary>中文区段 + 英文数字等（与 Everything 类文件名常见字符对齐）。</summary>
    private static readonly Regex SegmentTokens = new(
        @"[\p{IsCJKUnifiedIdeographs}\p{IsCJKSymbolsandPunctuation}\p{IsEnclosedCJKLettersandMonths}\p{IsCJKCompatibility}]+|[\w\.\-\+]+",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    internal static IReadOnlyList<string> CollectTokens(string? highlightNeedle)
    {
        var tokens = new List<string>();
        if (string.IsNullOrWhiteSpace(highlightNeedle)) return tokens;

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var trimmed = highlightNeedle.Trim();

        void Add(string token)
        {
            if (token.Length > 0 && seen.Add(token))
                tokens.Add(token);
        }

        foreach (var segment in trimmed.Split(
                     (char[]?)null,
                     StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var matches = SegmentTokens.Matches(segment);
            if (matches.Count == 0)
            {
                Add(segment);
                continue;
            }

            foreach (Match match in matches)
                Add(match.Value);
        }

        if (tokens.Count == 0)
            Add(trimmed);
        return tokens;
    }

    internal static (int Start, int End)[] GetRanges(
        string text,
        IReadOnlyList<string> tokens,
        string? mode)
    {
        var key = new CacheKey(
            text,
            string.Join('\u001f', tokens),
            PinyinFilterModes.Normalize(mode));
        if (RangeCache.TryGetValue(key, out var cached))
            return cached;

        var ranges = new List<(int Start, int End)>();
        AddTextRanges(text, tokens, ranges);
        SearchHighlightPinyinMatcher.AddRanges(text, tokens, ranges, mode);

        if (ranges.Count == 0)
            return Cache(key, []);

        ranges.Sort((left, right) => left.Start.CompareTo(right.Start));
        var merged = new List<(int Start, int End)>();
        foreach (var range in ranges)
        {
            if (merged.Count == 0)
            {
                merged.Add(range);
                continue;
            }

            var last = merged[^1];
            if (range.Start <= last.End)
                merged[^1] = (last.Start, Math.Max(last.End, range.End));
            else
                merged.Add(range);
        }

        return Cache(key, merged.ToArray());
    }

    private static void AddTextRanges(
        string text,
        IReadOnlyList<string> tokens,
        List<(int Start, int End)> ranges)
    {
        foreach (var token in tokens)
        {
            if (token.Length == 0) continue;
            var position = 0;
            while (position < text.Length)
            {
                var index = Compare.IndexOf(text, token, position, CompareOptions.OrdinalIgnoreCase);
                if (index < 0) break;
                ranges.Add((index, index + token.Length));
                position = index + 1;
            }
        }
    }

    private static (int Start, int End)[] Cache(CacheKey key, (int Start, int End)[] ranges)
    {
        RangeCache[key] = ranges;
        CacheOrder.Enqueue(key);
        while (RangeCache.Count > CacheCapacity && CacheOrder.TryDequeue(out var oldest))
            RangeCache.TryRemove(oldest, out _);
        return ranges;
    }

    private readonly record struct CacheKey(string Text, string Tokens, string Mode);
}
