using System.Buffers;
using System.Text;

namespace ClipboardX.Core;

/// <summary>
/// 为检索构建「全拼 + 首字母」连续小写串（去空格），与实时筛选输入配合。
/// </summary>
public static class PinyinSearchIndex
{
    /// <summary>全拼计算成本较高，仅取前缀参与转换，检索仍覆盖长文本前部。</summary>
    private const int MaxSourceChars = 8192;

    public static string BuildBlob(string text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        if (text.Length > MaxSourceChars)
            text = text[..MaxSourceChars];
        if (!HasCjk(text)) return "";
        try
        {
            var full = NPinyin.Pinyin.GetPinyin(text);
            return Normalize(full) + BuildInitialsFromFullPinyin(full);
        }
        catch
        {
            return "";
        }
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
        var w = 0;
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
}
