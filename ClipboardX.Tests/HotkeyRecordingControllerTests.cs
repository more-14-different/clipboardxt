using System.Windows.Input;
using System.Windows.Interop;
using ClipboardManager;

namespace ClipboardX.Tests;

public sealed class HotkeyRecordingControllerTests
{
    [Theory]
    [InlineData(Key.LeftCtrl)]
    [InlineData(Key.RightCtrl)]
    [InlineData(Key.LeftShift)]
    [InlineData(Key.RightShift)]
    [InlineData(Key.LeftAlt)]
    [InlineData(Key.RightAlt)]
    [InlineData(Key.LWin)]
    [InlineData(Key.RWin)]
    public void TryRecord_ModifierOnlyKey_ReturnsFalse(Key key)
    {
        var accepted = HotkeyRecordingController.TryRecord(
            key,
            Key.None,
            Win32.MOD_CONTROL,
            out var result);

        Assert.False(accepted);
        Assert.Equal(default, result);
    }

    [Fact]
    public void TryRecord_NoModifiers_ReturnsFalse()
    {
        var accepted = HotkeyRecordingController.TryRecord(Key.G, Key.None, 0, out var result);

        Assert.False(accepted);
        Assert.Equal(default, result);
    }

    [Fact]
    public void TryRecord_NoModifiersForPanelAction_ReturnsVirtualKey()
    {
        var accepted = HotkeyRecordingController.TryRecord(
            Key.F2,
            Key.None,
            0,
            out var result,
            allowNoModifiers: true);

        Assert.True(accepted);
        Assert.Equal(0u, result.Modifiers);
        Assert.Equal((uint)KeyInterop.VirtualKeyFromKey(Key.F2), result.Key);
    }

    [Fact]
    public void TryRecord_NormalKeyWithModifiers_ReturnsVirtualKey()
    {
        var modifiers = Win32.MOD_CONTROL | Win32.MOD_SHIFT;

        var accepted = HotkeyRecordingController.TryRecord(Key.G, Key.None, modifiers, out var result);

        Assert.True(accepted);
        Assert.Equal(modifiers, result.Modifiers);
        Assert.Equal((uint)KeyInterop.VirtualKeyFromKey(Key.G), result.Key);
    }

    [Fact]
    public void TryRecord_SystemKey_UsesSystemKey()
    {
        var accepted = HotkeyRecordingController.TryRecord(
            Key.System,
            Key.OemQuestion,
            Win32.MOD_ALT,
            out var result);

        Assert.True(accepted);
        Assert.Equal(Win32.MOD_ALT, result.Modifiers);
        Assert.Equal((uint)KeyInterop.VirtualKeyFromKey(Key.OemQuestion), result.Key);
    }
}
