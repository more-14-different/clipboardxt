using ClipboardManager;

namespace ClipboardX.Tests;

public sealed class ProcessNameCatalogTests
{
    [Fact]
    public void Filter_EmptyFilter_ReturnsInputOrder()
    {
        var result = ProcessNameCatalog.Filter(["Code", "explorer", "notepad"], "");

        Assert.Equal(["Code", "explorer", "notepad"], result);
    }

    [Fact]
    public void Filter_MatchesCaseInsensitiveSubstring()
    {
        var result = ProcessNameCatalog.Filter(["Code", "explorer", "notepad"], "EXP");

        Assert.Equal(["explorer"], result);
    }

    [Fact]
    public void Filter_TrimsFilterText()
    {
        var result = ProcessNameCatalog.Filter(["Code", "explorer", "notepad"], " pad ");

        Assert.Equal(["notepad"], result);
    }
}
