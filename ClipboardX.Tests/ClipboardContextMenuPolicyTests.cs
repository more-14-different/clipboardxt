using ClipboardManager;

namespace ClipboardX.Tests;

public sealed class ClipboardContextMenuPolicyTests
{
    [Fact]
    public void Evaluate_ShowsTextPostProcessingOnlyForMultiTextInNormalMode()
    {
        var selected = new[]
        {
            new ClipboardEntry { Type = EntryType.Text, TextContent = "one" },
            new ClipboardEntry { Type = EntryType.Text, TextContent = "two" },
        };

        var normal = ClipboardContextMenuPolicy.Evaluate(selected, BatchPasteQueueMode.Off);
        var fifo = ClipboardContextMenuPolicy.Evaluate(selected, BatchPasteQueueMode.Fifo);

        Assert.True(normal.ShowLinePaste);
        Assert.True(normal.ShowSoftLinePaste);
        Assert.False(fifo.ShowLinePaste);
        Assert.False(fifo.ShowSoftLinePaste);
    }

    [Fact]
    public void Evaluate_HidesTextPostProcessingForSingleOrMixedSelection()
    {
        var single = new[]
        {
            new ClipboardEntry { Type = EntryType.Text, TextContent = "one" },
        };
        var mixed = new[]
        {
            new ClipboardEntry { Type = EntryType.Text, TextContent = "one" },
            new ClipboardEntry { Type = EntryType.Files, FilePaths = [@"C:\one.txt"] },
        };

        Assert.False(ClipboardContextMenuPolicy.Evaluate(single, BatchPasteQueueMode.Off).ShowLinePaste);
        Assert.False(ClipboardContextMenuPolicy.Evaluate(mixed, BatchPasteQueueMode.Off).ShowLinePaste);
    }

    [Fact]
    public void Evaluate_ShowsUrlAndFileActionsFromWholeSelection()
    {
        var selected = new[]
        {
            new ClipboardEntry { Type = EntryType.Text, TextContent = "https://example.com/path" },
            new ClipboardEntry { Type = EntryType.Files, FilePaths = [@"C:\one.txt"] },
        };

        var state = ClipboardContextMenuPolicy.Evaluate(selected, BatchPasteQueueMode.Off);

        Assert.True(state.ShowOpenUrls);
        Assert.True(state.ShowPasteAsFile);
    }
}
