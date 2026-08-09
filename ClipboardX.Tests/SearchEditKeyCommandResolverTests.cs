using ClipboardManager;
using static ClipboardManager.SearchEditKeyCommandResolver;

namespace ClipboardX.Tests;

public sealed class SearchEditKeyCommandResolverTests
{
    [Theory]
    [InlineData(Win32.VK_V, (int)CommandKind.Paste)]
    [InlineData(Win32.VK_A, (int)CommandKind.SelectAll)]
    [InlineData(Win32.VK_C, (int)CommandKind.Copy)]
    [InlineData(Win32.VK_INSERT, (int)CommandKind.Copy)]
    [InlineData(Win32.VK_X, (int)CommandKind.Cut)]
    [InlineData(Win32.VK_Y, (int)CommandKind.Redo)]
    public void Resolve_MapsEditingCommands(uint key, int expected)
    {
        Assert.Equal((CommandKind)expected, Resolve(key, shift: false).Kind);
    }

    [Theory]
    [InlineData(Win32.VK_HOME, false, 0)]
    [InlineData(Win32.VK_END, true, 1)]
    public void Resolve_BoundaryPreservesShift(uint key, bool shift, int endMarker)
    {
        var command = Resolve(key, shift);

        Assert.Equal(CommandKind.MoveBoundary, command.Kind);
        Assert.Equal(endMarker, command.Value);
        Assert.Equal(shift, command.Shift);
    }

    [Theory]
    [InlineData(Win32.VK_LEFT, false, -1)]
    [InlineData(Win32.VK_RIGHT, true, 1)]
    public void Resolve_CaretMovementPreservesShift(uint key, bool shift, int direction)
    {
        var command = Resolve(key, shift);

        Assert.Equal(CommandKind.MoveCaret, command.Kind);
        Assert.Equal(direction, command.Value);
        Assert.Equal(shift, command.Shift);
    }

    [Theory]
    [InlineData(Win32.VK_BACK, -1)]
    [InlineData(Win32.VK_DELETE, 1)]
    public void Resolve_MapsUnitDeletion(uint key, int direction)
    {
        var command = Resolve(key, shift: true);

        Assert.Equal(CommandKind.Delete, command.Kind);
        Assert.Equal(direction, command.Value);
        Assert.False(command.Shift);
    }

    [Theory]
    [InlineData(false, (int)CommandKind.Undo)]
    [InlineData(true, (int)CommandKind.Redo)]
    public void Resolve_CtrlZUsesShiftForRedo(bool shift, int expected)
    {
        Assert.Equal((CommandKind)expected, Resolve(Win32.VK_Z, shift).Kind);
    }

    [Fact]
    public void Resolve_UnknownKeyIsNotHandled()
    {
        Assert.False(Resolve(0x51, shift: false).IsHandled);
    }
}
