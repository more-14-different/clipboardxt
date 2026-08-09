using ClipboardManager;

namespace ClipboardX.Tests;

public sealed class SearchQuerySpecTests
{
    [Theory]
    [InlineData(" da", "da value", true)]
    [InlineData(" da", " da value", false)]
    [InlineData("  da", " da value", true)]
    [InlineData("  da", "da value", false)]
    [InlineData("sdf ", "value sdf", true)]
    [InlineData("sdf ", "value sdf ", false)]
    [InlineData("sdf  ", "value sdf ", true)]
    [InlineData("sdf  ", "value sdf", false)]
    public void MatchesText_UsesOneOuterSpaceAsAnchor(string query, string item, bool expected)
    {
        var spec = SearchQuerySpec.Parse(query);

        Assert.Equal(expected, spec.MatchesText(item));
    }

    [Fact]
    public void MatchesText_AnchorsFirstAndLastTokensOnly()
    {
        var spec = SearchQuerySpec.Parse("  da asdio fjqwre def ");

        Assert.True(spec.MatchesText(" da xx fjqwre yy asdio zz def"));
        Assert.False(spec.MatchesText("da xx fjqwre yy asdio zz def"));
        Assert.False(spec.MatchesText(" da xx fjqwre yy asdio zz def "));
    }

    [Fact]
    public void Parse_LeadingExtraSpaceBelongsToStartNeedle()
    {
        var spec = SearchQuerySpec.Parse("  asdf   das   ffff 1");

        Assert.True(spec.AnchorStart);
        Assert.False(spec.AnchorEnd);
        Assert.Equal(" asdf", spec.StartNeedle);
        Assert.Equal(["das", "ffff", "1"], spec.ContainsTokens);
        Assert.True(spec.MatchesText(" asdf 1 das ffff"));
        Assert.False(spec.MatchesText("asdf 1 das ffff"));
    }

    [Fact]
    public void MatchesText_AllSpacesConsumesOuterAnchorsAndMatchesRemainderExactly()
    {
        var spec = SearchQuerySpec.Parse("    ");

        Assert.True(spec.AnchorStart);
        Assert.True(spec.AnchorEnd);
        Assert.Equal("  ", spec.ExactNeedle);
        Assert.True(spec.MatchesText("  "));
        Assert.False(spec.MatchesText("   "));
    }

    [Theory]
    [InlineData("d tauri ")]
    [InlineData(" d tauri ")]
    public void MatchesText_PathCanUseDriveTokenAndEndAnchor(string query)
    {
        var spec = SearchQuerySpec.Parse(query);

        Assert.True(spec.MatchesText(@"D:\C2D\Desktop\Code\Rust\sTools\komorebi-shortcuts-tauri"));
        Assert.False(spec.MatchesText(@"D:\C2D\Desktop\Code\Rust\sTools\komorebi-shortcuts-tauri\node_modules"));
    }
}
