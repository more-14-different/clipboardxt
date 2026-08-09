using ClipboardManager;

namespace ClipboardX.Tests;

public sealed class ForegroundOverlayPolicyTests
{
    [Theory]
    [InlineData("mousemaster")]
    [InlineData("Mousemaster")]
    [InlineData("MOUSEMASTER")]
    public void IsMousemasterProcessName_AcceptsMousemaster(string processName)
    {
        Assert.True(ForegroundOverlayPolicy.IsMousemasterProcessName(processName));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("mousemaster-helper")]
    [InlineData("othermousemaster")]
    public void IsMousemasterProcessName_RejectsOtherProcesses(string? processName)
    {
        Assert.False(ForegroundOverlayPolicy.IsMousemasterProcessName(processName));
    }
}
