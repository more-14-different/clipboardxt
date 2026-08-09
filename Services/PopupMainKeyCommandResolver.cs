using SearchEditCommand = ClipboardManager.SearchEditKeyCommandResolver.Command;

namespace ClipboardManager;

internal static class PopupMainKeyCommandResolver
{
    internal enum CommandKind
    {
        PassThrough,
        Swallow,
        TranslateCharacter,
        InsertCharacter,
        PasteClipboardIntoSearch,
        CutSearchSelection,
        SearchEdit,
        PasteByIndex,
        ToggleQuickPhraseFilter,
        TogglePointSelection,
        CommitSelection,
        MoveSelection,
        MoveSelectionByHjkl,
        MoveBoundary,
        ScrollPage,
        MoveCaret,
        TogglePreview,
        Escape,
        DeleteBackward,
        CycleTypeFilter,
        DeleteForwardOrItem,
    }

    internal readonly record struct Context(
        uint VirtualKey,
        bool Ctrl,
        bool Alt,
        bool Shift,
        bool PanelModifier);

    internal readonly record struct Command(
        CommandKind Kind,
        int Value = 0,
        bool Shift = false,
        bool NewlineAfterEachText = false,
        bool SoftLineBreakAfterEachText = false,
        char Character = '\0',
        SearchEditCommand SearchEdit = default)
    {
        internal bool IsPassThrough => Kind == CommandKind.PassThrough;
        internal bool IsSwallowedOnly => Kind == CommandKind.Swallow;
        internal bool NeedsCharacterTranslation => Kind == CommandKind.TranslateCharacter;
    }

    internal static Command Resolve(Context context)
    {
        var key = context.VirtualKey;
        if (context.Shift && !context.Ctrl && !context.Alt && key == Win32.VK_INSERT)
            return new Command(CommandKind.PasteClipboardIntoSearch);
        if (context.Shift && !context.Ctrl && !context.Alt && key == Win32.VK_DELETE)
            return new Command(CommandKind.CutSearchSelection);

        if (context.PanelModifier)
        {
            if (context.Ctrl && TryResolveCtrlCommand(context, out var ctrlCommand))
                return ctrlCommand;
            if (key is >= 0x31 and <= 0x39)
                return new Command(CommandKind.PasteByIndex, (int)(key - 0x30));
            if (key == 0x09)
                return new Command(CommandKind.ToggleQuickPhraseFilter);
            if (key == Win32.VK_RETURN)
                return ResolveEnter(context);
            return new Command(CommandKind.PassThrough);
        }

        if (key == Win32.VK_RETURN)
            return ResolveEnter(context);

        if (context.Ctrl && context.Alt)
            return new Command(CommandKind.PassThrough);
        if (context.Alt)
        {
            return key is 0x09 or 0x73
                ? new Command(CommandKind.PassThrough)
                : new Command(CommandKind.Swallow);
        }
        if (context.Ctrl)
        {
            return TryResolveCtrlCommand(context, out var ctrlCommand)
                ? ctrlCommand
                : new Command(CommandKind.PassThrough);
        }

        return key switch
        {
            Win32.VK_F1 => new Command(CommandKind.TogglePreview),
            Win32.VK_UP => new Command(CommandKind.MoveSelection, -1, Shift: context.Shift),
            Win32.VK_DOWN => new Command(CommandKind.MoveSelection, 1, Shift: context.Shift),
            Win32.VK_HOME => new Command(CommandKind.MoveBoundary, Shift: context.Shift),
            Win32.VK_END => new Command(CommandKind.MoveBoundary, 1, Shift: context.Shift),
            Win32.VK_PRIOR => new Command(CommandKind.ScrollPage, -1),
            Win32.VK_NEXT => new Command(CommandKind.ScrollPage, 1),
            Win32.VK_LEFT => new Command(CommandKind.MoveCaret, -1, Shift: context.Shift),
            Win32.VK_RIGHT => new Command(CommandKind.MoveCaret, 1, Shift: context.Shift),
            Win32.VK_ESCAPE => new Command(CommandKind.Escape),
            Win32.VK_BACK => new Command(CommandKind.DeleteBackward),
            0x09 => new Command(CommandKind.CycleTypeFilter),
            Win32.VK_DELETE => new Command(CommandKind.DeleteForwardOrItem),
            _ => new Command(CommandKind.TranslateCharacter),
        };
    }

    internal static Command ResolveCharacter(char? character)
    {
        return character.HasValue
            ? new Command(CommandKind.InsertCharacter, Character: character.Value)
            : new Command(CommandKind.Swallow);
    }

    private static Command ResolveEnter(Context context)
    {
        return context.Ctrl
            ? new Command(CommandKind.TogglePointSelection)
            : new Command(
                CommandKind.CommitSelection,
                NewlineAfterEachText: context.Alt,
                SoftLineBreakAfterEachText: context.Shift && !context.Alt);
    }

    private static bool TryResolveCtrlCommand(Context context, out Command command)
    {
        var searchEdit = SearchEditKeyCommandResolver.Resolve(context.VirtualKey, context.Shift);
        if (searchEdit.IsHandled)
        {
            command = new Command(CommandKind.SearchEdit, SearchEdit: searchEdit);
            return true;
        }

        var navigationDelta = KeyboardNavigationKeyResolver.ResolveHjklDelta(context.VirtualKey);
        if (navigationDelta.HasValue)
        {
            command = new Command(
                CommandKind.MoveSelectionByHjkl,
                navigationDelta.Value,
                Shift: context.Shift);
            return true;
        }

        command = default;
        return false;
    }
}
