using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using Brush = System.Windows.Media.Brush;

namespace ClipboardManager;

/// <summary>
/// 在 <see cref="InlineCollection"/> 中按搜索词分段高亮，供资源管理器快搜、剪切板弹窗、文件夹跳转共用。
/// </summary>
public static class SearchHighlightInlines
{
    public static void Append(
        InlineCollection inlines,
        string text,
        string? highlightNeedle,
        Brush normalForeground,
        Brush highlightForeground,
        double fontSize,
        FontWeight baseWeight,
        bool highlightSemiBold = true)
    {
        var tokens = CollectHighlightTokens(highlightNeedle);
        if (tokens.Count == 0)
        {
            AppendRun(inlines, text, normalForeground, fontSize, baseWeight);
            return;
        }

        var ranges = SearchHighlightMatcher.GetRanges(text, tokens, ClipboardEntry.PinyinFilterMode);
        if (ranges.Length == 0)
        {
            AppendRun(inlines, text, normalForeground, fontSize, baseWeight);
            return;
        }

        var cursor = 0;
        foreach (var (start, end) in ranges)
        {
            if (start > cursor)
                AppendRun(inlines, text[cursor..start], normalForeground, fontSize, baseWeight);

            AppendRun(
                inlines,
                text[start..end],
                highlightForeground,
                fontSize,
                highlightSemiBold ? FontWeights.SemiBold : baseWeight);
            cursor = end;
        }

        if (cursor < text.Length)
            AppendRun(inlines, text[cursor..], normalForeground, fontSize, baseWeight);
    }

    public static IReadOnlyList<string> CollectHighlightTokens(string? highlightNeedle) =>
        SearchHighlightMatcher.CollectTokens(highlightNeedle);

    private static void AppendRun(
        InlineCollection inlines,
        string text,
        Brush foreground,
        double fontSize,
        FontWeight fontWeight) =>
        inlines.Add(new Run(text)
        {
            Foreground = foreground,
            FontSize = fontSize,
            FontWeight = fontWeight,
        });
}
