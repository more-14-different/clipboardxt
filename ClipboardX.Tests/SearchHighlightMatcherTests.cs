using ClipboardManager;

namespace ClipboardX.Tests;

public sealed class SearchHighlightMatcherTests
{
    [Fact]
    public void CollectTokens_SplitsMixedSegmentsAndDeduplicatesIgnoringCase()
    {
        var tokens = SearchHighlightMatcher.CollectTokens("报告v2 报告 V2 foo-bar");

        Assert.Equal(["报告", "v2", "foo-bar"], tokens);
    }

    [Fact]
    public void GetRanges_MergesOverlappingTextMatches()
    {
        var ranges = SearchHighlightMatcher.GetRanges(
            "Alpha alphabet",
            ["alpha", "pha"],
            PinyinFilterModes.Traditional);

        Assert.Equal([(0, 5), (6, 11)], ranges);
    }

    [Theory]
    [InlineData("baogao")]
    [InlineData("bg")]
    public void GetRanges_MapsTraditionalPinyinToSourceCharacters(string token)
    {
        var ranges = SearchHighlightMatcher.GetRanges(
            "前报告后",
            [token],
            PinyinFilterModes.Traditional);

        Assert.Equal([(1, 3)], ranges);
    }

    [Fact]
    public void GetRanges_MapsXiaoheCodesToSourceCharacters()
    {
        var ranges = SearchHighlightMatcher.GetRanges(
            "前报告后",
            ["bcgc"],
            PinyinFilterModes.Xiaohe);

        Assert.Equal([(1, 3)], ranges);
    }

    [Fact]
    public void GetRanges_MergesAdjacentTextAndPinyinMatches()
    {
        var ranges = SearchHighlightMatcher.GetRanges(
            "报告report",
            ["bg", "report"],
            PinyinFilterModes.Traditional);

        Assert.Equal([(0, 8)], ranges);
    }
}
