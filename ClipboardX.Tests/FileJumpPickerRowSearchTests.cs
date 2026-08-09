using ClipboardManager;

namespace ClipboardX.Tests;

public sealed class FileJumpPickerRowSearchTests
{
    [Fact]
    public void OrderForDisplay_UsesRecentRankBeforeFavoriteTag()
    {
        var mostRecent = new FileJumpPickerRow("常用", @"D:\Recent", false,
            isRecentFolder: true, recentRank: 0);
        var olderFavorite = new FileJumpPickerRow("常用", @"D:\FavoriteRecent", true,
            isRecentFolder: true, recentRank: 1, favoriteRank: 0);
        var favoriteOnly = new FileJumpPickerRow("收藏", @"D:\FavoriteOnly", true,
            favoriteRank: 1);
        var contextOnly = new FileJumpPickerRow("explorer", @"D:\Context", false,
            contextRank: 0);

        var ordered = FileJumpPickerRowOrdering.OrderForDisplay(
            [contextOnly, olderFavorite, favoriteOnly, mostRecent]).ToList();

        Assert.Equal([mostRecent, olderFavorite, favoriteOnly, contextOnly], ordered);
    }

    [Fact]
    public void FavoriteRecentRow_ShowsCommonTagWithoutLosingFavorite()
    {
        var row = new FileJumpPickerRow("收藏", @"D:\Both", true, "work",
            isRecentFolder: true, recentRank: 0);

        Assert.True(row.IsFavorite);
        Assert.True(row.IsRecentFolder);
        Assert.Contains(row.MetadataChips, chip => chip.Text == "常用");
        Assert.Contains(row.MetadataChips, chip => chip.Text == "work");
    }

    [Theory]
    [InlineData("常用")]
    [InlineData("收藏")]
    public void MatchesSearch_DoesNotIndexFilterOnlySystemTags(string query)
    {
        var source = query;
        var row = new FileJumpPickerRow(
            source,
            @"D:\Projects",
            isFavorite: source == "收藏",
            isRecentFolder: source == "常用");

        Assert.False(row.MatchesSearch(query));
    }

    [Fact]
    public void MatchesSearch_StillIndexesFavoritePhrase()
    {
        var row = new FileJumpPickerRow(
            "收藏",
            @"D:\Projects",
            isFavorite: true,
            phrase: "workspace");

        Assert.True(row.MatchesSearch("workspace"));
    }

    [Theory]
    [InlineData("标签")]
    [InlineData("文件夹")]
    public void MatchesSearch_DoesNotIndexInfoFieldLabels(string query)
    {
        var row = new FileJumpPickerRow(
            "收藏",
            @"D:\Projects",
            isFavorite: true,
            phrase: "workspace");

        Assert.False(row.MatchesSearch(query));
    }

    [Theory]
    [InlineData("d tauri ")]
    [InlineData(" d tauri ")]
    public void MatchesSearch_AnchorsAgainstPathBeforeMetadata(string query)
    {
        var row = new FileJumpPickerRow(
            "explorer",
            @"D:\C2D\Desktop\Code\Rust\sTools\komorebi-shortcuts-tauri",
            isFavorite: false,
            phrase: "work");

        Assert.True(row.MatchesSearch(query));
    }

    [Theory]
    [InlineData("d tauri ")]
    [InlineData(" d tauri ")]
    public void MatchesSearch_EndAnchorIgnoresTrailingDirectorySeparator(string query)
    {
        var row = new FileJumpPickerRow(
            "explorer",
            @"D:\C2D\Desktop\Code\Rust\sTools\komorebi-shortcuts-tauri\",
            isFavorite: false);

        Assert.True(row.MatchesSearch(query));
    }
}
