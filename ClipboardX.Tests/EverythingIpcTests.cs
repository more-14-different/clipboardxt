using ClipboardManager;

namespace ClipboardX.Tests;

public sealed class EverythingFolderQueryBuilderTests
{
    [Fact]
    public void Build_UsesEndAnchorAndUnanchoredTokensOnce()
    {
        var spec = SearchQuerySpec.Parse("d tauri ");

        var query = EverythingFolderQueryBuilder.Build(spec);

        Assert.Equal("folder: endwith:\"tauri\" \"d\"", query);
        Assert.True(spec.MatchesText(@"D:\C2D\Desktop\Code\Rust\sTools\komorebi-shortcuts-tauri"));
        Assert.False(spec.MatchesText(@"D:\C2D\Desktop\Code\Rust\sTools\komorebi-shortcuts-tauri\node_modules"));
    }

    [Fact]
    public void Build_UsesOnlyDriveAndEndAnchors()
    {
        var spec = SearchQuerySpec.Parse(" d tauri ");

        var query = EverythingFolderQueryBuilder.Build(spec);

        Assert.Equal("folder: d: endwith:\"tauri\"", query);
    }

    [Fact]
    public void Build_UsesAllUnanchoredTokens()
    {
        var spec = SearchQuerySpec.Parse("d tauri");

        var query = EverythingFolderQueryBuilder.Build(spec);

        Assert.Equal("folder: \"d\" \"tauri\"", query);
    }

    [Fact]
    public void Build_UsesDriveAnchorAndRemainingToken()
    {
        var spec = SearchQuerySpec.Parse(" d tauri");

        var query = EverythingFolderQueryBuilder.Build(spec);

        Assert.Equal("folder: d: \"tauri\"", query);
    }

    [Fact]
    public void Build_EmptyQuery_UsesFolderFilterOnly()
    {
        var query = EverythingFolderQueryBuilder.Build(SearchQuerySpec.Parse(""));

        Assert.Equal("folder:", query);
    }

    [Fact]
    public void Build_EscapesQuotesAndRemovesDuplicateClauses()
    {
        var query = EverythingFolderQueryBuilder.Build(SearchQuerySpec.Parse("a\"b A\"B"));

        Assert.Equal("folder: \"a\\\"b\"", query);
    }

    [Theory]
    [InlineData(int.MinValue, 500)]
    [InlineData(0, 500)]
    [InlineData(50, 500)]
    [InlineData(51, 510)]
    [InlineData(500, 5000)]
    [InlineData(int.MaxValue, 5000)]
    public void CandidateQueryMax_ExpandsAndClampsWithoutOverflow(int requested, int expected)
    {
        Assert.Equal(expected, EverythingFolderQueryBuilder.CandidateQueryMax(requested));
    }
}
