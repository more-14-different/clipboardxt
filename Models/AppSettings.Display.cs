using System.IO;
using System.Text.Json;

namespace ClipboardManager;

public partial class AppSettings
{    public string HotkeyDisplayName => FormatHotkey(HotkeyModifiers, HotkeyKey);

    public string FileJumpHotkeyDisplayName => FormatHotkey(FileJumpHotkeyModifiers, FileJumpHotkeyKey);

    public string BatchModeCycleHotkeyDisplayName => FormatHotkey(BatchModeCycleHotkeyModifiers, BatchModeCycleHotkeyKey);

    public string StarToggleHotkeyDisplayName => FormatHotkey(StarToggleHotkeyModifiers, StarToggleHotkeyKey);

    public string ClipboardPasteHotkeyDisplayName => FormatHotkey(ClipboardPasteHotkeyModifiers, ClipboardPasteHotkeyKey);
    public string ClipboardPasteAsFileHotkeyDisplayName => FormatHotkey(ClipboardPasteAsFileHotkeyModifiers, ClipboardPasteAsFileHotkeyKey);
    public string ClipboardPasteJsonHotkeyDisplayName => FormatHotkey(ClipboardPasteJsonHotkeyModifiers, ClipboardPasteJsonHotkeyKey);
    public string ClipboardEditTextHotkeyDisplayName => FormatHotkey(ClipboardEditTextHotkeyModifiers, ClipboardEditTextHotkeyKey);
    public string ClipboardShortcutPhraseHotkeyDisplayName => FormatHotkey(ClipboardShortcutPhraseHotkeyModifiers, ClipboardShortcutPhraseHotkeyKey);
    public string ClipboardDeleteHotkeyDisplayName => FormatHotkey(ClipboardDeleteHotkeyModifiers, ClipboardDeleteHotkeyKey);
    public string FileJumpFavoriteHotkeyDisplayName => FormatHotkey(FileJumpFavoriteHotkeyModifiers, FileJumpFavoriteHotkeyKey);
    public string FileJumpEditPhraseHotkeyDisplayName => FormatHotkey(FileJumpEditPhraseHotkeyModifiers, FileJumpEditPhraseHotkeyKey);
    public string FileJumpRemoveRecentHotkeyDisplayName => FormatHotkey(FileJumpRemoveRecentHotkeyModifiers, FileJumpRemoveRecentHotkeyKey);

    /// <summary>用于设置中单键展示（无修饰键）。</summary>
    public static string FormatSingleVk(uint vk) => VkToName(vk);

    public static string FormatHotkey(uint modifiers, uint key)
    {
        var parts = new List<string>();
        if ((modifiers & Win32.MOD_CONTROL) != 0) parts.Add("Ctrl");
        if ((modifiers & Win32.MOD_SHIFT) != 0) parts.Add("Shift");
        if ((modifiers & Win32.MOD_ALT) != 0) parts.Add("Alt");
        if ((modifiers & Win32.MOD_WIN) != 0) parts.Add("Win");
        if ((modifiers & Win32.MOD_CAPS) != 0) parts.Add("CapsLock");
        parts.Add(VkToName(key));
        return string.Join("+", parts);
    }

    private static string VkToName(uint vk) => vk switch
    {
        0xC0 => "`", 0xBD => "-", 0xBB => "=", 0xDB => "[", 0xDD => "]",
        0xDC => "\\", 0xBA => ";", 0xDE => "'", 0xBC => ",", 0xBE => ".",
        0xBF => "/", 0x08 => "Backspace", 0x09 => "Tab", 0x0D => "Enter",
        0x1B => "Esc", 0x20 => "Space", 0x21 => "PageUp", 0x22 => "PageDown",
        0x23 => "End", 0x24 => "Home", 0x25 => "←", 0x26 => "↑", 0x27 => "→",
        0x28 => "↓", 0x2D => "Insert", 0x2E => "Delete",
        0x6B => "Num +", 0x6D => "Num -",
        >= 0x60 and <= 0x69 => $"Num {vk - 0x60}",
        >= 0x70 and <= 0x7B => $"F{vk - 0x6F}",
        >= 0x30 and <= 0x39 => ((char)vk).ToString(),
        >= 0x41 and <= 0x5A => ((char)vk).ToString(),
        _ => $"0x{vk:X2}"
    };
}

