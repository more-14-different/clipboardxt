using System.Buffers;
using System.Text;

namespace ClipboardManager;

/// <summary>
/// 为检索构建拼音连续小写串（去空格），与键盘钩子里的 ASCII 输入配合，无需 IME、不抢前台焦点。
/// </summary>
internal static class PinyinSearchIndex
{
    /// <summary>全拼计算成本较高，仅取前缀参与转换，检索仍覆盖长文本前部。</summary>
    private const int MaxSourceChars = 8192;

    public static string BuildBlob(string text, string? mode = null)
    {
        if (string.IsNullOrEmpty(text)) return "";
        if (text.Length > MaxSourceChars)
            text = text[..MaxSourceChars];
        if (!HasCjk(text)) return "";
        try
        {
            var full = NPinyin.Pinyin.GetPinyin(text);
            return PinyinFilterModes.Normalize(mode) == PinyinFilterModes.Xiaohe
                ? BuildXiaoheBlob(full)
                : Normalize(full) + BuildInitialsFromFullPinyin(full);
        }
        catch
        {
            return "";
        }
    }

    public static bool MatchesToken(string text, string token, string? mode = null)
    {
        token = Normalize(token);
        if (string.IsNullOrEmpty(text) || token.Length == 0 || !HasCjk(text)) return false;
        return PinyinFilterModes.Normalize(mode) == PinyinFilterModes.Xiaohe
            ? MatchesXiaoheToken(text, token)
            : BuildBlob(text, PinyinFilterModes.Traditional).Contains(token, StringComparison.OrdinalIgnoreCase);
    }

    public static bool HasCjk(string text)
    {
        foreach (var c in text)
        {
            if (c is >= '\u4e00' and <= '\u9fff') return true;
            if (c is >= '\u3400' and <= '\u4dbf') return true; // CJK 扩展 A
        }
        return false;
    }

    private static string Normalize(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        char[]? rented = null;
        Span<char> buf = s.Length <= 512
            ? stackalloc char[s.Length]
            : (rented = ArrayPool<char>.Shared.Rent(s.Length)).AsSpan(0, s.Length);
        int w = 0;
        try
        {
            foreach (var c in s)
            {
                if (c is ' ' or '\t' or '\'' or '’') continue;
                buf[w++] = char.ToLowerInvariant(c);
            }

            return w == 0 ? "" : new string(buf[..w]);
        }
        finally
        {
            if (rented != null) ArrayPool<char>.Shared.Return(rented);
        }
    }

    private static string BuildInitialsFromFullPinyin(string fullPinyin)
    {
        if (string.IsNullOrWhiteSpace(fullPinyin)) return "";

        var sb = new StringBuilder(fullPinyin.Length / 4);
        var takeNext = true;
        foreach (var c in fullPinyin)
        {
            if (c is ' ' or '\t' or '\'' or '’')
            {
                takeNext = true;
                continue;
            }

            if (!takeNext) continue;
            sb.Append(char.ToLowerInvariant(c));
            takeNext = false;
        }

        return sb.ToString();
    }

    private static string BuildXiaoheBlob(string fullPinyin)
    {
        if (string.IsNullOrWhiteSpace(fullPinyin)) return "";
        var parts = fullPinyin.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var sb = new System.Text.StringBuilder(parts.Length * 3);
        foreach (var part in parts)
            sb.Append(ToXiaoheCode(part));
        foreach (var part in parts)
            sb.Append(ToXiaoheInitial(part));
        return sb.ToString();
    }

    internal static string ToXiaoheCode(string syllable)
    {
        syllable = Normalize(syllable).Replace("ü", "v");
        if (syllable.Length == 0) return "";
        if (syllable.Length == 1) return syllable;

        var initial = "";
        var final = syllable;
        if (syllable.StartsWith("zh", StringComparison.Ordinal))
        {
            initial = "v";
            final = syllable[2..];
        }
        else if (syllable.StartsWith("ch", StringComparison.Ordinal))
        {
            initial = "i";
            final = syllable[2..];
        }
        else if (syllable.StartsWith("sh", StringComparison.Ordinal))
        {
            initial = "u";
            final = syllable[2..];
        }
        else if ("bpmfdtnlgkhjqxrzcsyw".Contains(syllable[0]))
        {
            initial = syllable[0].ToString();
            final = syllable[1..];
        }
        else
        {
            return ToXiaoheZeroInitialCode(syllable);
        }

        return initial + XiaoheFinalKey(final);
    }

    internal static string ToXiaoheInitial(string syllable)
    {
        syllable = Normalize(syllable).Replace("ü", "v");
        if (syllable.Length == 0) return "";
        if (syllable.StartsWith("zh", StringComparison.Ordinal)) return "v";
        if (syllable.StartsWith("ch", StringComparison.Ordinal)) return "i";
        if (syllable.StartsWith("sh", StringComparison.Ordinal)) return "u";
        return syllable[0].ToString();
    }

    private static string XiaoheFinalKey(string final) => final switch
    {
        "a" => "a",
        "o" or "uo" => "o",
        "e" => "e",
        "i" => "i",
        "u" => "u",
        "v" or "ve" or "ue" => "t",
        "ai" => "d",
        "ei" => "w",
        "ui" or "uei" => "v",
        "ao" => "c",
        "ou" => "z",
        "iu" or "iou" => "q",
        "ie" => "p",
        "er" => "r",
        "an" => "j",
        "en" => "f",
        "in" => "b",
        "un" or "uen" or "vn" => "y",
        "ang" => "h",
        "eng" => "g",
        "ing" => "k",
        "ong" or "iong" => "s",
        "ia" or "ua" => "x",
        "uai" => "k",
        "ian" => "m",
        "uan" => "r",
        "iang" or "uang" => "l",
        "iao" => "n",
        _ => final.Length > 0 ? final[^1].ToString() : ""
    };

    private static string ToXiaoheZeroInitialCode(string syllable)
    {
        if (syllable.Length <= 2) return syllable;
        var first = syllable[0].ToString();
        var rest = syllable[1..];
        return first + XiaoheFinalKey(rest);
    }

    private static bool MatchesXiaoheToken(string text, string token)
    {
        var i = 0;
        while (i < text.Length)
        {
            if (!HasCjkChar(text[i]))
            {
                i++;
                continue;
            }

            var start = i;
            while (i < text.Length && HasCjkChar(text[i])) i++;

            var parts = NPinyin.Pinyin.GetPinyin(text[start..i])
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var codes = new string[parts.Length];
            var initials = new StringBuilder(parts.Length);
            for (var p = 0; p < parts.Length; p++)
            {
                codes[p] = ToXiaoheCode(parts[p]);
                initials.Append(ToXiaoheInitial(parts[p]));
            }

            if (MatchesWholeXiaoheCodes(codes, token)) return true;
            if (initials.ToString().Contains(token, StringComparison.OrdinalIgnoreCase)) return true;
        }

        return false;
    }

    private static bool MatchesWholeXiaoheCodes(IReadOnlyList<string> codes, string token)
    {
        var needle = token.AsSpan();
        for (var start = 0; start < codes.Count; start++)
        {
            var offset = 0;
            for (var i = start; i < codes.Count; i++)
            {
                var code = codes[i].AsSpan();
                if (offset + code.Length > needle.Length) break;
                if (!needle[offset..].StartsWith(code, StringComparison.OrdinalIgnoreCase)) break;

                offset += code.Length;
                if (offset == needle.Length)
                    return true;
            }
        }

        return false;
    }

    private static bool HasCjkChar(char c) =>
        c is >= '\u4e00' and <= '\u9fff'
        || c is >= '\u3400' and <= '\u4dbf';
}
