using ClipboardManager;

namespace ClipboardX.Tests;

public sealed class FileDialogClassSummaryTests
{
    [Fact]
    public void Observe_StopsWhenHighestPriorityDirectUiKindIsKnown()
    {
        var summary = new FileDialogClassSummary();

        Assert.True(summary.Observe("DirectUIHWND"));
        Assert.True(summary.Observe("ToolbarWindow32"));
        Assert.False(summary.Observe("Edit"));
        Assert.Equal(FileDialogKind.GeneralDirectUi, summary.Classify());
    }

    [Fact]
    public void Observe_ContinuesPastSysListViewCandidateForPossibleDirectUi()
    {
        var summary = new FileDialogClassSummary();

        Assert.True(summary.Observe("SysListView32"));
        Assert.True(summary.Observe("ToolbarWindow32"));
        Assert.True(summary.Observe("Edit"));
        Assert.Equal(FileDialogKind.SysListView, summary.Classify());

        Assert.False(summary.Observe("DirectUIHWND"));
        Assert.Equal(FileDialogKind.GeneralDirectUi, summary.Classify());
    }

    [Fact]
    public void Classify_RecognizesShellDefViewFallback()
    {
        var summary = new FileDialogClassSummary();

        Assert.True(summary.Observe("SHELLDLL_DefView"));

        Assert.Equal(FileDialogKind.ShellDefViewOrGeneral, summary.Classify());
    }

    [Fact]
    public void Classify_PreservesCaseInsensitiveLegacyClassMatching()
    {
        var summary = new FileDialogClassSummary();

        summary.Observe("syslistview32");
        summary.Observe("toolbarwindow32");
        summary.Observe("edit");

        Assert.Equal(FileDialogKind.SysListView, summary.Classify());
    }
}
