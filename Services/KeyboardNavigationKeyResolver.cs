namespace ClipboardManager;

internal static class KeyboardNavigationKeyResolver
{
    internal static int? ResolveHjklDelta(uint virtualKey)
    {
        return virtualKey switch
        {
            0x48 => -3,
            0x4A => 1,
            0x4B => -1,
            0x4C => 3,
            _ => null,
        };
    }
}
