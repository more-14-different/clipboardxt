using System;
using System.Collections.Generic;
using System.Linq;

namespace ClipboardManager;

internal sealed class SearchQuerySpec
{
    private SearchQuerySpec(
        string raw,
        bool anchorStart,
        bool anchorEnd,
        string? startNeedle,
        string? endNeedle,
        string? exactNeedle,
        string[] containsTokens,
        string[] broadTokens)
    {
        Raw = raw;
        AnchorStart = anchorStart;
        AnchorEnd = anchorEnd;
        StartNeedle = startNeedle;
        EndNeedle = endNeedle;
        ExactNeedle = exactNeedle;
        ContainsTokens = containsTokens;
        BroadTokens = broadTokens;
    }

    public string Raw { get; }
    public bool AnchorStart { get; }
    public bool AnchorEnd { get; }
    public string? StartNeedle { get; }
    public string? EndNeedle { get; }
    public string? ExactNeedle { get; }
    public string[] ContainsTokens { get; }
    public string[] BroadTokens { get; }
    public bool IsEmpty => !AnchorStart && !AnchorEnd && ContainsTokens.Length == 0;

    public static SearchQuerySpec Parse(string? query)
    {
        var raw = query ?? "";
        if (raw.Length == 0)
            return new SearchQuerySpec(raw, false, false, null, null, null, [], []);

        var anchorStart = char.IsWhiteSpace(raw[0]);
        var anchorEnd = char.IsWhiteSpace(raw[^1]);

        var bodyStart = anchorStart ? 1 : 0;
        var bodyEnd = raw.Length - (anchorEnd ? 1 : 0);
        if (bodyEnd < bodyStart) bodyEnd = bodyStart;
        var body = raw[bodyStart..bodyEnd];

        if (body.Length == 0)
            return new SearchQuerySpec(
                raw,
                anchorStart,
                anchorEnd,
                anchorStart ? "" : null,
                anchorEnd ? "" : null,
                anchorStart && anchorEnd ? "" : null,
                [],
                []);

        var tokens = TokenizeBody(body);
        var broadTokens = tokens
            .Select(t => t.Trim())
            .Where(t => t.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (tokens.Length == 0)
        {
            var needle = body;
            string[] broad = string.IsNullOrWhiteSpace(needle) ? [] : [needle.Trim()];
            return new SearchQuerySpec(
                raw,
                anchorStart,
                anchorEnd,
                anchorStart ? needle : null,
                anchorEnd ? needle : null,
                anchorStart && anchorEnd ? needle : null,
                anchorStart || anchorEnd ? [] : [needle],
                broad);
        }

        var startNeedle = anchorStart ? tokens[0] : null;
        var endNeedle = anchorEnd ? tokens[^1] : null;
        var contains = new List<string>(tokens.Length);
        for (var i = 0; i < tokens.Length; i++)
        {
            if (anchorStart && i == 0) continue;
            if (anchorEnd && i == tokens.Length - 1) continue;
            contains.Add(tokens[i]);
        }

        return new SearchQuerySpec(
            raw,
            anchorStart,
            anchorEnd,
            startNeedle,
            endNeedle,
            null,
            contains.ToArray(),
            broadTokens);
    }

    public bool MatchesText(string searchable)
    {
        searchable ??= "";
        if (ExactNeedle != null)
            return string.Equals(searchable, ExactNeedle, StringComparison.OrdinalIgnoreCase);

        if (AnchorStart && !searchable.StartsWith(StartNeedle ?? "", StringComparison.OrdinalIgnoreCase))
            return false;
        if (AnchorEnd && !searchable.EndsWith(EndNeedle ?? "", StringComparison.OrdinalIgnoreCase))
            return false;

        foreach (var token in ContainsTokens)
        {
            if (!searchable.Contains(token, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        return true;
    }

    public bool MatchesTextOrPinyin(string searchable, Func<string, string, bool> tokenMatches)
        => MatchesTextOrPinyin(searchable, searchable, tokenMatches);

    public bool MatchesTextOrPinyin(
        string searchable,
        string anchorSearchable,
        Func<string, string, bool> tokenMatches)
    {
        searchable ??= "";
        anchorSearchable ??= "";
        if (ExactNeedle != null)
            return string.Equals(anchorSearchable, ExactNeedle, StringComparison.OrdinalIgnoreCase);

        if (AnchorStart && !anchorSearchable.StartsWith(StartNeedle ?? "", StringComparison.OrdinalIgnoreCase))
            return false;
        if (AnchorEnd && !anchorSearchable.EndsWith(EndNeedle ?? "", StringComparison.OrdinalIgnoreCase))
            return false;

        foreach (var token in ContainsTokens)
        {
            if (!tokenMatches(searchable, token))
                return false;
        }

        return true;
    }

    private static string[] TokenizeBody(string body)
    {
        var tokens = new List<string>();
        var i = 0;
        while (i < body.Length)
        {
            if (char.IsWhiteSpace(body[i]))
            {
                i++;
                continue;
            }

            var start = i;
            while (i < body.Length && !char.IsWhiteSpace(body[i]))
                i++;

            var tokenStart = start;
            if (tokens.Count == 0)
            {
                tokenStart = 0;
                while (tokenStart < start && !char.IsWhiteSpace(body[tokenStart]))
                    tokenStart++;
            }

            tokens.Add(body[tokenStart..i]);
        }

        if (tokens.Count > 0)
        {
            var last = tokens.Count - 1;
            var end = body.Length;
            var token = tokens[last];
            var tokenEnd = body.LastIndexOf(token, StringComparison.Ordinal) + token.Length;
            if (tokenEnd >= token.Length && tokenEnd < end)
                tokens[last] = body[(tokenEnd - token.Length)..end];
        }

        return tokens.ToArray();
    }
}
