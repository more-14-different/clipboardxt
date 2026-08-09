using ClipboardManager;
using static ClipboardManager.FileJumpPickerKeyCommandResolver;

namespace ClipboardX.Tests;

public sealed class FileJumpPickerKeyCommandResolverTests
{
    [Fact]
    public void Resolve_CustomHotkeyTakesPriorityOverBuiltInShortcut()
    {
        var command = Resolve(CreateContext(
            Win32.VK_V,
            ctrl: true,
            favoriteHotkeyMatch: true));

        Assert.Equal(CommandKind.ToggleFavorite, command.Kind);
    }

    [Theory]
    [InlineData(Win32.VK_V, true, false, false, (int)CommandKind.PasteClipboardIntoSearch)]
    [InlineData(Win32.VK_INSERT, false, false, true, (int)CommandKind.PasteClipboardIntoSearch)]
    [InlineData(Win32.VK_INSERT, true, false, false, (int)CommandKind.CopySearchSelection)]
    [InlineData(Win32.VK_DELETE, false, false, true, (int)CommandKind.CutSearchSelection)]
    public void Resolve_MapsClipboardEditingShortcuts(
        uint key,
        bool ctrl,
        bool alt,
        bool shift,
        int expected)
    {
        var command = Resolve(CreateContext(key, ctrl, alt, shift));

        Assert.Equal((CommandKind)expected, command.Kind);
    }

    [Theory]
    [InlineData(0x31u, (int)CommandKind.JumpQuickIndex, 1)]
    [InlineData(0x69u, (int)CommandKind.JumpQuickIndex, 9)]
    [InlineData(0x09u, (int)CommandKind.ToggleFavoritesFilter, 0)]
    [InlineData(0xBBu, (int)CommandKind.ScrollPage, 1)]
    [InlineData(0x6Du, (int)CommandKind.ScrollPage, -1)]
    public void Resolve_MapsPanelModifierCommands(uint key, int expected, int value)
    {
        var command = Resolve(CreateContext(key, panelModifierMatch: true));

        Assert.Equal((CommandKind)expected, command.Kind);
        Assert.Equal(value, command.Value);
        Assert.False(command.FlushSearch);
    }

    [Theory]
    [InlineData(0x4Au, 1)]
    [InlineData(0x4Bu, -1)]
    [InlineData(0x48u, -3)]
    [InlineData(0x4Cu, 3)]
    public void Resolve_MapsCtrlHjklToSelectionDelta(uint key, int expectedDelta)
    {
        var command = Resolve(CreateContext(key, ctrl: true));

        Assert.Equal(CommandKind.MoveSelection, command.Kind);
        Assert.Equal(expectedDelta, command.Value);
    }

    [Theory]
    [InlineData(Win32.VK_HOME, false, 0)]
    [InlineData(Win32.VK_END, true, 1)]
    public void Resolve_CtrlBoundaryPreservesShift(uint key, bool shift, int endMarker)
    {
        var command = Resolve(CreateContext(key, ctrl: true, shift: shift));

        Assert.Equal(CommandKind.MoveBoundary, command.Kind);
        Assert.Equal(endMarker, command.Value);
        Assert.Equal(shift, command.Shift);
    }

    [Theory]
    [InlineData(false, (int)CommandKind.UndoSearchEdit)]
    [InlineData(true, (int)CommandKind.RedoSearchEdit)]
    public void Resolve_CtrlZUsesShiftForRedo(bool shift, int expected)
    {
        var command = Resolve(CreateContext(Win32.VK_Z, ctrl: true, shift: shift));

        Assert.Equal((CommandKind)expected, command.Kind);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Resolve_EnterDistinguishesNormalAndPasteCommit(bool ctrl)
    {
        var command = Resolve(CreateContext(Win32.VK_RETURN, ctrl: ctrl));

        Assert.Equal(CommandKind.CommitSelection, command.Kind);
        Assert.Equal(ctrl, command.PasteText);
    }

    [Theory]
    [InlineData(Win32.VK_LEFT, false, false, -1)]
    [InlineData(Win32.VK_RIGHT, false, true, 1)]
    [InlineData(Win32.VK_LEFT, true, true, -1)]
    [InlineData(Win32.VK_RIGHT, true, false, 1)]
    public void Resolve_MapsCaretMovement(uint key, bool ctrl, bool shift, int direction)
    {
        var command = Resolve(CreateContext(key, ctrl: ctrl, shift: shift));

        Assert.Equal(CommandKind.MoveSearchCaret, command.Kind);
        Assert.Equal(direction, command.Value);
        Assert.Equal(ctrl, command.Ctrl);
        Assert.Equal(shift, command.Shift);
    }

    [Theory]
    [InlineData(Win32.VK_BACK)]
    [InlineData(Win32.VK_DELETE)]
    public void Resolve_UnmodifiedDeleteKeysRequireSearchText(uint key)
    {
        Assert.False(Resolve(CreateContext(key)).IsHandled);

        var command = Resolve(CreateContext(key, hasSearchText: true));

        Assert.Equal(CommandKind.DeleteSearch, command.Kind);
    }

    [Fact]
    public void Resolve_UnknownCtrlOrAltShortcutIsNotHandled()
    {
        Assert.False(Resolve(CreateContext(0x51, ctrl: true)).IsHandled);
        Assert.False(Resolve(CreateContext(0x51, alt: true)).IsHandled);
    }

    [Theory]
    [InlineData(Win32.VK_PRIOR, -1)]
    [InlineData(Win32.VK_NEXT, 1)]
    public void Resolve_UnmodifiedPageKeyFlushesPendingSearch(uint key, int direction)
    {
        var command = Resolve(CreateContext(key));

        Assert.Equal(CommandKind.ScrollPage, command.Kind);
        Assert.Equal(direction, command.Value);
        Assert.True(command.FlushSearch);
    }

    [Theory]
    [InlineData(Win32.VK_F1, (int)CommandKind.TogglePreview)]
    [InlineData(Win32.VK_ESCAPE, (int)CommandKind.Escape)]
    [InlineData(0x09u, (int)CommandKind.ToggleFavoritesFilter)]
    public void Resolve_MapsWindowCommands(uint key, int expected)
    {
        Assert.Equal((CommandKind)expected, Resolve(CreateContext(key)).Kind);
    }

    [Theory]
    [InlineData('x', true)]
    [InlineData(' ', true)]
    [InlineData('\n', false)]
    public void ResolveCharacter_OnlyHandlesPrintableCharacters(char character, bool handled)
    {
        var command = ResolveCharacter(character);

        Assert.Equal(handled, command.IsHandled);
        if (handled)
            Assert.Equal(character, command.Character);
    }

    private static Context CreateContext(
        uint key,
        bool ctrl = false,
        bool alt = false,
        bool shift = false,
        bool panelModifierMatch = false,
        bool favoriteHotkeyMatch = false,
        bool editPhraseHotkeyMatch = false,
        bool removeRecentHotkeyMatch = false,
        bool hasSearchText = false) =>
        new(
            key,
            ctrl,
            alt,
            shift,
            panelModifierMatch,
            favoriteHotkeyMatch,
            editPhraseHotkeyMatch,
            removeRecentHotkeyMatch,
            hasSearchText);
}
