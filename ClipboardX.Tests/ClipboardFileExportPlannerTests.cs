using ClipboardManager;

namespace ClipboardX.Tests;

public sealed class ClipboardFileExportPlannerTests
{
    [Fact]
    public void Build_ExpandsMixedEntriesInInputOrder()
    {
        var entries = new[]
        {
            new ClipboardEntry { Type = EntryType.Text, TextContent = "notes" },
            new ClipboardEntry { Type = EntryType.Text, TextContent = "{\"ok\":true}" },
            new ClipboardEntry { Type = EntryType.Image, ImageData = [1, 2, 3] },
            new ClipboardEntry { Type = EntryType.Files, FilePaths = [@"C:\one.txt", @"C:\two.txt"] },
        };

        var plan = ClipboardFileExportPlanner.Build(entries);

        Assert.Collection(
            plan,
            item => Assert.Equal(ClipboardFileExportPlanner.ItemKind.Text, item.Kind),
            item => Assert.Equal(ClipboardFileExportPlanner.ItemKind.Json, item.Kind),
            item => Assert.Equal(ClipboardFileExportPlanner.ItemKind.Png, item.Kind),
            item => Assert.Equal(@"C:\one.txt", item.ExistingPath),
            item => Assert.Equal(@"C:\two.txt", item.ExistingPath));
    }

    [Fact]
    public void Build_SkipsEmptyOrUnavailableContent()
    {
        var entries = new[]
        {
            new ClipboardEntry { Type = EntryType.Text, TextContent = "" },
            new ClipboardEntry { Type = EntryType.Image },
            new ClipboardEntry { Type = EntryType.Files, FilePaths = ["", "  "] },
        };

        Assert.Empty(ClipboardFileExportPlanner.Build(entries));
    }

    [Theory]
    [InlineData((int)ClipboardFileExportPlanner.ItemKind.Text, ".txt")]
    [InlineData((int)ClipboardFileExportPlanner.ItemKind.Json, ".json")]
    [InlineData((int)ClipboardFileExportPlanner.ItemKind.Png, ".png")]
    public void BuildTempFileName_IsOrderedUniqueAndUsesKindExtension(
        int kindValue,
        string extension)
    {
        var kind = (ClipboardFileExportPlanner.ItemKind)kindValue;
        var name = ClipboardFileExportPlanner.BuildTempFileName(
            new DateTime(2026, 8, 16, 12, 34, 56, 789),
            "abc12345",
            7,
            kind);

        Assert.Equal($"clip_20260816_123456_789_007_abc12345{extension}", name);
    }

    [Theory]
    [InlineData("{\"value\":1}", true)]
    [InlineData("[1,2,3]", true)]
    [InlineData("{\"value\":1,}", false)]
    [InlineData("// comment\n{}", false)]
    [InlineData("plain text", false)]
    public void IsWellFormedJson_RemainsStrict(string text, bool expected)
    {
        Assert.Equal(expected, ClipboardFileExportPlanner.IsWellFormedJson(text));
    }
}
