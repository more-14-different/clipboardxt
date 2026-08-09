using System.Text;

namespace ClipboardManager;

internal static class SearchHighlightPinyinMatcher
{
    internal static void AddRanges(
        string text,
        IReadOnlyList<string> tokens,
        List<(int Start, int End)> ranges,
        string? mode)
    {
        if (string.IsNullOrEmpty(text) || tokens.Count == 0) return;

        var normalizedTokens = tokens
            .Select(NormalizeToken)
            .Where(token => token.Length > 0)
            .ToArray();
        if (normalizedTokens.Length == 0) return;

        var position = 0;
        while (position < text.Length)
        {
            if (!IsCjk(text[position]))
            {
                position++;
                continue;
            }

            var start = position;
            while (position < text.Length && IsCjk(text[position])) position++;
            AddRangesForCjkRun(text, start, position, normalizedTokens, ranges, mode);
        }
    }

    private static void AddRangesForCjkRun(
        string text,
        int runStart,
        int runEnd,
        IReadOnlyList<string> tokens,
        List<(int Start, int End)> ranges,
        string? mode)
    {
        var full = new StringBuilder();
        var initials = new StringBuilder();
        var xiaoheInitials = new StringBuilder();
        var xiaoheCodes = new List<string>(runEnd - runStart);
        var fullEnds = new List<int>(runEnd - runStart);
        var initialEnds = new List<int>(runEnd - runStart);
        var xiaoheInitialEnds = new List<int>(runEnd - runStart);
        var charPositions = new List<int>(runEnd - runStart);
        mode = PinyinFilterModes.Normalize(mode);

        for (var i = runStart; i < runEnd; i++)
        {
            var value = text[i].ToString();
            var pinyin = NormalizeToken(NPinyin.Pinyin.GetPinyin(value));
            var initial = NormalizeToken(NPinyin.Pinyin.GetInitials(value));
            if (pinyin.Length == 0 && initial.Length == 0) continue;

            full.Append(pinyin);
            initials.Append(initial);
            fullEnds.Add(full.Length);
            initialEnds.Add(initials.Length);
            if (mode == PinyinFilterModes.Xiaohe)
            {
                var code = PinyinSearchIndex.ToXiaoheCode(pinyin);
                xiaoheCodes.Add(code);
                xiaoheInitials.Append(PinyinSearchIndex.ToXiaoheInitial(pinyin));
                xiaoheInitialEnds.Add(xiaoheInitials.Length);
            }
            charPositions.Add(i);
        }

        if (charPositions.Count == 0) return;

        foreach (var token in tokens)
        {
            if (mode == PinyinFilterModes.Xiaohe)
            {
                AddWholeSyllableCodeRanges(xiaoheCodes, token, charPositions, ranges);
                AddBlobRanges(xiaoheInitials.ToString(), token, xiaoheInitialEnds, charPositions, ranges);
            }
            else
            {
                AddBlobRanges(full.ToString(), token, fullEnds, charPositions, ranges);
                AddBlobRanges(initials.ToString(), token, initialEnds, charPositions, ranges);
            }
        }
    }

    private static void AddWholeSyllableCodeRanges(
        IReadOnlyList<string> codes,
        string token,
        IReadOnlyList<int> charPositions,
        List<(int Start, int End)> ranges)
    {
        if (codes.Count == 0 || token.Length == 0) return;
        for (var start = 0; start < codes.Count; start++)
        {
            var length = 0;
            var candidate = new StringBuilder(token.Length + 2);
            for (var i = start; i < codes.Count; i++)
            {
                candidate.Append(codes[i]);
                length += codes[i].Length;
                if (length == token.Length
                    && candidate.ToString().Equals(token, StringComparison.OrdinalIgnoreCase))
                {
                    ranges.Add((charPositions[start], charPositions[i] + 1));
                    break;
                }
                if (length >= token.Length) break;
            }
        }
    }

    private static void AddBlobRanges(
        string blob,
        string token,
        IReadOnlyList<int> charEndOffsets,
        IReadOnlyList<int> charPositions,
        List<(int Start, int End)> ranges)
    {
        if (blob.Length == 0 || token.Length == 0 || token.Length > blob.Length) return;
        var position = 0;
        while (position < blob.Length)
        {
            var index = blob.IndexOf(token, position, StringComparison.OrdinalIgnoreCase);
            if (index < 0) break;

            var startChar = FindCharByPinyinOffset(charEndOffsets, index);
            var endChar = FindCharByPinyinOffset(charEndOffsets, index + token.Length - 1);
            if (startChar >= 0 && endChar >= startChar)
                ranges.Add((charPositions[startChar], charPositions[endChar] + 1));

            position = index + 1;
        }
    }

    private static int FindCharByPinyinOffset(IReadOnlyList<int> charEndOffsets, int offset)
    {
        for (var i = 0; i < charEndOffsets.Count; i++)
        {
            if (offset < charEndOffsets[i]) return i;
        }
        return charEndOffsets.Count - 1;
    }

    private static string NormalizeToken(string value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        Span<char> buffer = stackalloc char[value.Length];
        var length = 0;
        foreach (var character in value)
        {
            if (character is ' ' or '\t' or '\'' or '’') continue;
            if (character is >= 'A' and <= 'Z')
                buffer[length++] = (char)(character + 32);
            else if (character is >= 'a' and <= 'z')
                buffer[length++] = character;
        }
        return length == 0 ? "" : new string(buffer[..length]);
    }

    private static bool IsCjk(char character) =>
        character is >= '\u4e00' and <= '\u9fff'
        || character is >= '\u3400' and <= '\u4dbf';
}
