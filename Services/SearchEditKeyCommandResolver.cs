namespace ClipboardManager;

internal static class SearchEditKeyCommandResolver
{
    internal enum CommandKind
    {
        None,
        MoveBoundary,
        Paste,
        SelectAll,
        Copy,
        Cut,
        Redo,
        Undo,
        MoveCaret,
        Delete,
    }

    internal readonly record struct Command(
        CommandKind Kind,
        int Value = 0,
        bool Shift = false)
    {
        internal bool IsHandled => Kind != CommandKind.None;
    }

    internal static Command Resolve(uint virtualKey, bool shift)
    {
        return virtualKey switch
        {
            Win32.VK_HOME => new Command(CommandKind.MoveBoundary, Shift: shift),
            Win32.VK_END => new Command(CommandKind.MoveBoundary, Value: 1, Shift: shift),
            Win32.VK_V => new Command(CommandKind.Paste),
            Win32.VK_A => new Command(CommandKind.SelectAll),
            Win32.VK_C or Win32.VK_INSERT => new Command(CommandKind.Copy),
            Win32.VK_X => new Command(CommandKind.Cut),
            Win32.VK_Y => new Command(CommandKind.Redo),
            Win32.VK_Z => new Command(shift ? CommandKind.Redo : CommandKind.Undo),
            Win32.VK_LEFT => new Command(CommandKind.MoveCaret, Value: -1, Shift: shift),
            Win32.VK_RIGHT => new Command(CommandKind.MoveCaret, Value: 1, Shift: shift),
            Win32.VK_BACK => new Command(CommandKind.Delete, Value: -1),
            Win32.VK_DELETE => new Command(CommandKind.Delete, Value: 1),
            _ => default,
        };
    }
}
