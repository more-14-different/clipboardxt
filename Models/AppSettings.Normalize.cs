using System.IO;
using System.Text.Json;

namespace ClipboardManager;

public partial class AppSettings
{    public static void NormalizePopupPanelSettings(AppSettings s)
    {
        s.KeyPassthroughRules ??= new List<KeyPassthroughRule>();
        if (s.MaxImageItems < 0 || s.MaxImageItems > 5000)
            s.MaxImageItems = 150;
        if (s.MaxImageSizeBytes <= 0)
            s.MaxImageSizeBytes = 15 * 1024 * 1024;
        if (s.PopupPanelWidth < 280 || s.PopupPanelWidth > 1200 || double.IsNaN(s.PopupPanelWidth))
            s.PopupPanelWidth = 420;
        if (s.PopupPanelMaxHeight < 200 || s.PopupPanelMaxHeight > 900 || double.IsNaN(s.PopupPanelMaxHeight))
            s.PopupPanelMaxHeight = 560;
        if (s.PopupPanelHeight < 0 || double.IsNaN(s.PopupPanelHeight))
            s.PopupPanelHeight = 0;
        if (s.FileJumpPickerWidth < 280 || s.FileJumpPickerWidth > 1200 || double.IsNaN(s.FileJumpPickerWidth))
            s.FileJumpPickerWidth = 520;
        if (s.FileJumpPickerMaxHeight < 200 || s.FileJumpPickerMaxHeight > 900 || double.IsNaN(s.FileJumpPickerMaxHeight))
            s.FileJumpPickerMaxHeight = 560;
        if (s.FileJumpPickerHeight < 0 || double.IsNaN(s.FileJumpPickerHeight))
            s.FileJumpPickerHeight = 0;
        if (s.ExplorerQuickFindWidth < 280 || s.ExplorerQuickFindWidth > 1200 || double.IsNaN(s.ExplorerQuickFindWidth))
            s.ExplorerQuickFindWidth = 540;
        if (s.ExplorerQuickFindMaxHeight < 200 || s.ExplorerQuickFindMaxHeight > 900 || double.IsNaN(s.ExplorerQuickFindMaxHeight))
            s.ExplorerQuickFindMaxHeight = 520;
        if (s.ExplorerQuickFindHeight < 0 || double.IsNaN(s.ExplorerQuickFindHeight))
            s.ExplorerQuickFindHeight = 0;
        if (s.PopupPageItems < 1 || s.PopupPageItems > 50)
            s.PopupPageItems = 8;
        if (s.PanelPageScrollUpModifiers == 0)
            s.PanelPageScrollUpModifiers = Win32.MOD_CONTROL;
        if (s.PanelPageScrollDownModifiers == 0)
            s.PanelPageScrollDownModifiers = Win32.MOD_CONTROL;
        if (s.PanelPageScrollUpKey == 0)
            s.PanelPageScrollUpKey = 0xBD;
        if (s.PanelPageScrollDownKey == 0)
            s.PanelPageScrollDownKey = 0xBB;
        if (s.StarToggleHotkeyModifiers == 0)
            s.StarToggleHotkeyModifiers = Win32.MOD_CONTROL;
        if (s.StarToggleHotkeyKey == 0)
            s.StarToggleHotkeyKey = 0x44;
        if (s.ClipboardPasteHotkeyKey == 0) s.ClipboardPasteHotkeyKey = Win32.VK_RETURN;
        if (s.ClipboardPasteAsFileHotkeyKey == 0) s.ClipboardPasteAsFileHotkeyKey = Win32.VK_RETURN;
        if (s.ClipboardPasteJsonHotkeyKey == 0) s.ClipboardPasteJsonHotkeyKey = Win32.VK_RETURN;
        if (s.ClipboardEditTextHotkeyKey == 0) s.ClipboardEditTextHotkeyKey = 0x71;
        if (s.ClipboardShortcutPhraseHotkeyKey == 0) s.ClipboardShortcutPhraseHotkeyKey = 0x53;
        if (s.ClipboardDeleteHotkeyKey == 0) s.ClipboardDeleteHotkeyKey = Win32.VK_DELETE;
        if (s.FileJumpFavoriteHotkeyKey == 0) s.FileJumpFavoriteHotkeyKey = 0x44;
        if (s.FileJumpEditPhraseHotkeyKey == 0) s.FileJumpEditPhraseHotkeyKey = 0x71;
        if (s.FileJumpRemoveRecentHotkeyKey == 0) s.FileJumpRemoveRecentHotkeyKey = Win32.VK_DELETE;
        if (s.PanelPageScrollUpModifiers == s.PanelPageScrollDownModifiers
            && s.PanelPageScrollUpKey == s.PanelPageScrollDownKey)
        {
            s.PanelPageScrollUpKey = 0xBD;
            s.PanelPageScrollDownKey = 0xBB;
        }
    }
}

