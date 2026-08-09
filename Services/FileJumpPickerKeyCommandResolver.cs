namespace ClipboardManager;

internal static class FileJumpPickerKeyCommandResolver
{
    internal enum CommandKind
    {
        None,
        ToggleFavorite,
        EditPhrase,
        RemoveRecent,
        PasteClipboardIntoSearch,
        CopySearchSelection,
        CutSearchSelection,
        JumpQuickIndex,
        ToggleFavoritesFilter,
        ScrollPage,
        MoveBoundary,
        SelectAllSearchText,
        RedoSearchEdit,
        UndoSearchEdit,
        MoveSearchCaret,
        DeleteSearch,
        MoveSelection,
        CommitSelection,
        TogglePreview,
        Escape,
        InsertCharacter,
    }

    internal readonly record struct Context(
        uint VirtualKey,
        bool Ctrl,
        bool Alt,
        bool Shift,
        bool PanelModifierMatch,
        bool FavoriteHotkeyMatch,
        bool EditPhraseHotkeyMatch,
        bool RemoveRecentHotkeyMatch,
        bool HasSearchText);

    internal readonly record struct Command(
        CommandKind Kind,
        int Value = 0,
        bool Ctrl = false,
        bool Shift = false,
        bool PasteText = false,
        char Character = '\0',
        bool FlushSearch = false)
    {
        internal bool IsHandled => Kind != CommandKind.None;
    }

    internal static Command Resolve(Context context)
    {
        var key = context.VirtualKey;
        if (context.FavoriteHotkeyMatch)
            return new Command(CommandKind.ToggleFavorite);
        if (context.EditPhraseHotkeyMatch)
            return new Command(CommandKind.EditPhrase);
        if (context.RemoveRecentHotkeyMatch)
            return new Command(CommandKind.RemoveRecent);

        if (context.Shift && !context.Ctrl && !context.Alt && key == Win32.VK_INSERT)
        {
            return new Command(CommandKind.PasteClipboardIntoSearch);
        }
        if (context.Shift && !context.Ctrl && !context.Alt && key == Win32.VK_DELETE)
            return new Command(CommandKind.CutSearchSelection);

        if (context.PanelModifierMatch)
        {
            if (key is >= 0x31 and <= 0x39)
                return new Command(CommandKind.JumpQuickIndex, (int)(key - 0x30));
            if (key is >= 0x61 and <= 0x69)
                return new Command(CommandKind.JumpQuickIndex, (int)(key - 0x60));
            if (key == 0x09)
                return new Command(CommandKind.ToggleFavoritesFilter);
            if (key is 0xBB or 0x6B)
                return new Command(CommandKind.ScrollPage, 1);
            if (key is 0xBD or 0x6D)
                return new Command(CommandKind.ScrollPage, -1);
        }

        if (context.Ctrl && !context.Alt)
        {
            var searchEditCommand = SearchEditKeyCommandResolver.Resolve(key, context.Shift);
            if (searchEditCommand.IsHandled)
                return MapSearchEditCommand(searchEditCommand);

            var navigationDelta = KeyboardNavigationKeyResolver.ResolveHjklDelta(key);
            if (navigationDelta.HasValue)
                return new Command(CommandKind.MoveSelection, navigationDelta.Value);
            if (key == Win32.VK_RETURN)
                return new Command(CommandKind.CommitSelection, PasteText: true);
        }

        if (context.Ctrl || context.Alt)
            return default;

        return key switch
        {
            Win32.VK_UP => new Command(CommandKind.MoveSelection, -1),
            Win32.VK_DOWN => new Command(CommandKind.MoveSelection, 1),
            Win32.VK_LEFT => new Command(CommandKind.MoveSearchCaret, -1, Shift: context.Shift),
            Win32.VK_RIGHT => new Command(CommandKind.MoveSearchCaret, 1, Shift: context.Shift),
            Win32.VK_HOME => new Command(CommandKind.MoveBoundary, 0, Shift: context.Shift),
            Win32.VK_END => new Command(CommandKind.MoveBoundary, 1, Shift: context.Shift),
            Win32.VK_PRIOR => new Command(CommandKind.ScrollPage, -1, FlushSearch: true),
            Win32.VK_NEXT => new Command(CommandKind.ScrollPage, 1, FlushSearch: true),
            Win32.VK_F1 => new Command(CommandKind.TogglePreview),
            Win32.VK_RETURN => new Command(CommandKind.CommitSelection),
            Win32.VK_ESCAPE => new Command(CommandKind.Escape),
            Win32.VK_BACK when context.HasSearchText => new Command(CommandKind.DeleteSearch, -1),
            Win32.VK_DELETE when context.HasSearchText => new Command(CommandKind.DeleteSearch, 1),
            0x09 => new Command(CommandKind.ToggleFavoritesFilter),
            _ => default,
        };
    }

    private static Command MapSearchEditCommand(SearchEditKeyCommandResolver.Command command)
    {
        return command.Kind switch
        {
            SearchEditKeyCommandResolver.CommandKind.MoveBoundary =>
                new Command(CommandKind.MoveBoundary, command.Value, Shift: command.Shift),
            SearchEditKeyCommandResolver.CommandKind.Paste =>
                new Command(CommandKind.PasteClipboardIntoSearch),
            SearchEditKeyCommandResolver.CommandKind.SelectAll =>
                new Command(CommandKind.SelectAllSearchText),
            SearchEditKeyCommandResolver.CommandKind.Copy =>
                new Command(CommandKind.CopySearchSelection),
            SearchEditKeyCommandResolver.CommandKind.Cut =>
                new Command(CommandKind.CutSearchSelection),
            SearchEditKeyCommandResolver.CommandKind.Redo =>
                new Command(CommandKind.RedoSearchEdit),
            SearchEditKeyCommandResolver.CommandKind.Undo =>
                new Command(CommandKind.UndoSearchEdit),
            SearchEditKeyCommandResolver.CommandKind.MoveCaret =>
                new Command(CommandKind.MoveSearchCaret, command.Value, Ctrl: true, Shift: command.Shift),
            SearchEditKeyCommandResolver.CommandKind.Delete =>
                new Command(CommandKind.DeleteSearch, command.Value, Ctrl: true),
            _ => default,
        };
    }

    internal static Command ResolveCharacter(char? character)
    {
        return character is { } value && !char.IsControl(value)
            ? new Command(CommandKind.InsertCharacter, Character: value)
            : default;
    }
}
