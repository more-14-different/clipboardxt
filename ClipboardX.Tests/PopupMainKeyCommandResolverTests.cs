using ClipboardManager;
using static ClipboardManager.PopupMainKeyCommandResolver;

namespace ClipboardX.Tests;

public sealed class PopupMainKeyCommandResolverTests
{
    [Theory]
    [InlineData(Win32.VK_INSERT, false, true, (int)CommandKind.PasteClipboardIntoSearch)]
    [InlineData(Win32.VK_DELETE, false, true, (int)CommandKind.CutSearchSelection)]
    public void Resolve_MapsShiftClipboardShortcuts(uint key, bool ctrl, bool shift, int expected)
    {
        Assert.Equal((CommandKind)expected, Resolve(ContextFor(key, ctrl: ctrl, shift: shift)).Kind);
    }

    [Theory]
    [InlineData(0x31u, (int)CommandKind.PasteByIndex, 1)]
    [InlineData(0x39u, (int)CommandKind.PasteByIndex, 9)]
    [InlineData(0x09u, (int)CommandKind.ToggleQuickPhraseFilter, 0)]
    public void Resolve_MapsPanelCommands(uint key, int expected, int value)
    {
        var command = Resolve(ContextFor(key, panelModifier: true));

        Assert.Equal((CommandKind)expected, command.Kind);
        Assert.Equal(value, command.Value);
    }

    [Fact]
    public void Resolve_PanelCtrlEditingPrecedesPanelCommands()
    {
        var command = Resolve(ContextFor(
            Win32.VK_V,
            ctrl: true,
            panelModifier: true));

        Assert.Equal(CommandKind.SearchEdit, command.Kind);
        Assert.Equal(SearchEditKeyCommandResolver.CommandKind.Paste, command.SearchEdit.Kind);
    }

    [Theory]
    [InlineData(false, false, (int)CommandKind.CommitSelection, false, false)]
    [InlineData(false, true, (int)CommandKind.CommitSelection, false, true)]
    [InlineData(true, false, (int)CommandKind.TogglePointSelection, false, false)]
    public void Resolve_MapsEnter(
        bool ctrl,
        bool shift,
        int expected,
        bool newline,
        bool softLineBreak)
    {
        var command = Resolve(ContextFor(Win32.VK_RETURN, ctrl: ctrl, shift: shift));

        Assert.Equal((CommandKind)expected, command.Kind);
        Assert.Equal(newline, command.NewlineAfterEachText);
        Assert.Equal(softLineBreak, command.SoftLineBreakAfterEachText);
    }

    [Fact]
    public void Resolve_AltEnterCommitsWithNewline()
    {
        var command = Resolve(ContextFor(Win32.VK_RETURN, alt: true, shift: true));

        Assert.Equal(CommandKind.CommitSelection, command.Kind);
        Assert.True(command.NewlineAfterEachText);
        Assert.False(command.SoftLineBreakAfterEachText);
    }

    [Fact]
    public void Resolve_CtrlAltEnterStillTogglesPointSelection()
    {
        var command = Resolve(ContextFor(Win32.VK_RETURN, ctrl: true, alt: true));

        Assert.Equal(CommandKind.TogglePointSelection, command.Kind);
    }

    [Fact]
    public void Resolve_AltPanelEnterCommitsWithNewline()
    {
        var command = Resolve(ContextFor(
            Win32.VK_RETURN,
            alt: true,
            panelModifier: true));

        Assert.Equal(CommandKind.CommitSelection, command.Kind);
        Assert.True(command.NewlineAfterEachText);
    }

    [Theory]
    [InlineData(0x09u, true)]
    [InlineData(0x73u, true)]
    [InlineData(0x51u, false)]
    public void Resolve_AltKeysPassOnlySystemCombinations(uint key, bool passThrough)
    {
        var command = Resolve(ContextFor(key, alt: true));

        Assert.Equal(passThrough, command.IsPassThrough);
        Assert.Equal(!passThrough, command.IsSwallowedOnly);
    }

    [Fact]
    public void Resolve_CtrlAltPassesThrough()
    {
        Assert.True(Resolve(ContextFor(0x51, ctrl: true, alt: true)).IsPassThrough);
    }

    [Fact]
    public void Resolve_UnknownCtrlKeyPassesThrough()
    {
        Assert.True(Resolve(ContextFor(0x51, ctrl: true)).IsPassThrough);
    }

    [Theory]
    [InlineData(0x48u, -3)]
    [InlineData(0x4Au, 1)]
    [InlineData(0x4Bu, -1)]
    [InlineData(0x4Cu, 3)]
    public void Resolve_MapsCtrlHjkl(uint key, int delta)
    {
        var command = Resolve(ContextFor(key, ctrl: true, shift: true));

        Assert.Equal(CommandKind.MoveSelectionByHjkl, command.Kind);
        Assert.Equal(delta, command.Value);
        Assert.True(command.Shift);
    }

    [Theory]
    [InlineData(Win32.VK_UP, -1)]
    [InlineData(Win32.VK_DOWN, 1)]
    public void Resolve_MapsSelectionMovement(uint key, int direction)
    {
        var command = Resolve(ContextFor(key, shift: true));

        Assert.Equal(CommandKind.MoveSelection, command.Kind);
        Assert.Equal(direction, command.Value);
        Assert.True(command.Shift);
    }

    [Theory]
    [InlineData(Win32.VK_HOME, (int)CommandKind.MoveBoundary, 0)]
    [InlineData(Win32.VK_END, (int)CommandKind.MoveBoundary, 1)]
    [InlineData(Win32.VK_PRIOR, (int)CommandKind.ScrollPage, -1)]
    [InlineData(Win32.VK_NEXT, (int)CommandKind.ScrollPage, 1)]
    [InlineData(Win32.VK_LEFT, (int)CommandKind.MoveCaret, -1)]
    [InlineData(Win32.VK_RIGHT, (int)CommandKind.MoveCaret, 1)]
    public void Resolve_MapsDirectionalCommands(uint key, int expected, int value)
    {
        var command = Resolve(ContextFor(key, shift: true));

        Assert.Equal((CommandKind)expected, command.Kind);
        Assert.Equal(value, command.Value);
    }

    [Theory]
    [InlineData(Win32.VK_F1, (int)CommandKind.TogglePreview)]
    [InlineData(Win32.VK_ESCAPE, (int)CommandKind.Escape)]
    [InlineData(Win32.VK_BACK, (int)CommandKind.DeleteBackward)]
    [InlineData(0x09u, (int)CommandKind.CycleTypeFilter)]
    [InlineData(Win32.VK_DELETE, (int)CommandKind.DeleteForwardOrItem)]
    public void Resolve_MapsMainCommands(uint key, int expected)
    {
        Assert.Equal((CommandKind)expected, Resolve(ContextFor(key)).Kind);
    }

    [Fact]
    public void Resolve_UnknownUnmodifiedKeyRequiresCharacterTranslation()
    {
        Assert.True(Resolve(ContextFor(0x51)).NeedsCharacterTranslation);
    }

    [Theory]
    [InlineData('q', (int)CommandKind.InsertCharacter)]
    [InlineData(null, (int)CommandKind.Swallow)]
    public void ResolveCharacter_MapsTranslationResult(char? character, int expected)
    {
        Assert.Equal((CommandKind)expected, ResolveCharacter(character).Kind);
    }

    [Fact]
    public void Resolve_UnknownPanelKeyPassesThrough()
    {
        Assert.True(Resolve(ContextFor(0x51, panelModifier: true)).IsPassThrough);
    }

    private static Context ContextFor(
        uint key,
        bool ctrl = false,
        bool alt = false,
        bool shift = false,
        bool panelModifier = false) =>
        new(key, ctrl, alt, shift, panelModifier);
}
