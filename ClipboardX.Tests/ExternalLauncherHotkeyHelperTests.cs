using ClipboardManager;

namespace ClipboardX.Tests;

public sealed class ExternalLauncherHotkeyHelperTests
{
    [Theory]
    [InlineData(0x7C)] // F13
    [InlineData(0x7F)] // F16
    [InlineData(0x80)] // F17
    [InlineData(0x81)] // F18
    public void IsTriggerKey_AcceptsPrivateLauncherRange(uint key)
    {
        Assert.True(ExternalLauncherHotkeyHelper.IsTriggerKey(key));
    }

    [Theory]
    [InlineData(0x7B)] // F12
    [InlineData(0x82)] // F19
    [InlineData(0x1B)] // Escape
    public void IsTriggerKey_RejectsClipboardAndSystemKeys(uint key)
    {
        Assert.False(ExternalLauncherHotkeyHelper.IsTriggerKey(key));
    }
}
