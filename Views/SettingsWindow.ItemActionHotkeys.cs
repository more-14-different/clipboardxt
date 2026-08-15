using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace ClipboardManager;

public partial class SettingsWindow : Window
{
    private void InitializeItemActionHotkeys(AppSettings settings)
    {
        Add("ClipboardPaste", settings.ClipboardPasteHotkeyModifiers, settings.ClipboardPasteHotkeyKey, ClipboardPasteHotkeyText);
        Add("ClipboardPasteAsFile", settings.ClipboardPasteAsFileHotkeyModifiers, settings.ClipboardPasteAsFileHotkeyKey, ClipboardPasteAsFileHotkeyText);
        Add("ClipboardPasteJson", settings.ClipboardPasteJsonHotkeyModifiers, settings.ClipboardPasteJsonHotkeyKey, ClipboardPasteJsonHotkeyText);
        Add("ClipboardEditText", settings.ClipboardEditTextHotkeyModifiers, settings.ClipboardEditTextHotkeyKey, ClipboardEditTextHotkeyText);
        Add("ClipboardShortcutPhrase", settings.ClipboardShortcutPhraseHotkeyModifiers, settings.ClipboardShortcutPhraseHotkeyKey, ClipboardShortcutPhraseHotkeyText);
        Add("ClipboardDelete", settings.ClipboardDeleteHotkeyModifiers, settings.ClipboardDeleteHotkeyKey, ClipboardDeleteHotkeyText);
        Add("FileJumpFavorite", settings.FileJumpFavoriteHotkeyModifiers, settings.FileJumpFavoriteHotkeyKey, FileJumpFavoriteHotkeyText);
        Add("FileJumpEditPhrase", settings.FileJumpEditPhraseHotkeyModifiers, settings.FileJumpEditPhraseHotkeyKey, FileJumpEditPhraseHotkeyText);
        Add("FileJumpRemoveRecent", settings.FileJumpRemoveRecentHotkeyModifiers, settings.FileJumpRemoveRecentHotkeyKey, FileJumpRemoveRecentHotkeyText);

        void Add(string name, uint modifiers, uint key, TextBlock display)
        {
            _pendingItemActionHotkeys[name] = (modifiers, key);
            display.Text = AppSettings.FormatHotkey(modifiers, key);
        }
    }

    private void ItemActionHotkeyBox_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Border { Tag: string name } box || box.Child is not TextBlock display) return;
        _recordingItemActionHotkey = name;
        display.Text = "按下快捷键…";
        display.Foreground = (System.Windows.Media.Brush)FindResource("AccentBg");
        box.Focus();
    }

    private void ItemActionHotkeyBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not Border { Tag: string name } box || box.Child is not TextBlock display) return;
        if (_recordingItemActionHotkey != name) return;
        e.Handled = true;
        if (!HotkeyRecordingController.TryRecord(
                e.Key,
                e.SystemKey,
                GetHotkeyModifiersForRecording(),
                out var result,
                allowNoModifiers: true))
            return;

        _pendingItemActionHotkeys[name] = (result.Modifiers, result.Key);
        _recordingItemActionHotkey = null;
        display.Text = AppSettings.FormatHotkey(result.Modifiers, result.Key);
        display.Foreground = (System.Windows.Media.Brush)FindResource("PrimaryText");
    }

    private void ItemActionHotkeyBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is not Border { Tag: string name } box || box.Child is not TextBlock display) return;
        if (_recordingItemActionHotkey != name) return;
        _recordingItemActionHotkey = null;
        var binding = _pendingItemActionHotkeys[name];
        display.Text = AppSettings.FormatHotkey(binding.Modifiers, binding.Key);
        display.Foreground = (System.Windows.Media.Brush)FindResource("PrimaryText");
    }

    private string? FindItemActionHotkeyConflict()
    {
#if CLIPX_CLIPBOARD
        var clipboard = new List<(string Label, uint Modifiers, uint Key)>
        {
            Item("粘贴", "ClipboardPaste"),
            Item("图片/文本/文件作为文件粘贴", "ClipboardPasteAsFile"),
            Item("粘贴为 JSON 文件", "ClipboardPasteJson"),
            Item("编辑文本", "ClipboardEditText"),
            Item("设为/修改快捷短语", "ClipboardShortcutPhrase"),
            ("收藏/取消收藏", _pendingStarToggleModifiers, _pendingStarToggleKey),
            Item("删除", "ClipboardDelete")
        };
        var clipboardConflict = Find(clipboard, "剪贴板");
        if (clipboardConflict != null) return clipboardConflict;
#endif
#if CLIPX_FILEJUMP
        var fileJump = new List<(string Label, uint Modifiers, uint Key)>
        {
            Item("加入/移除收藏", "FileJumpFavorite"),
            Item("修改关键词", "FileJumpEditPhrase"),
            Item("移除常用路径", "FileJumpRemoveRecent")
        };
        var fileJumpConflict = Find(fileJump, "文件跳转");
        if (fileJumpConflict != null) return fileJumpConflict;
#endif
        return null;

        (string Label, uint Modifiers, uint Key) Item(string label, string name)
        {
            var binding = _pendingItemActionHotkeys[name];
            return (label, binding.Modifiers, binding.Key);
        }

        static string? Find(List<(string Label, uint Modifiers, uint Key)> actions, string panel)
        {
            for (var i = 0; i < actions.Count; i++)
            for (var j = i + 1; j < actions.Count; j++)
                if (actions[i].Modifiers == actions[j].Modifiers && actions[i].Key == actions[j].Key)
                    return $"{panel}条目动作“{actions[i].Label}”与“{actions[j].Label}”不能使用相同快捷键。";
            return null;
        }
    }

    private void ApplyItemActionHotkeys(AppSettings settings)
    {
        var v = _pendingItemActionHotkeys;
        (settings.ClipboardPasteHotkeyModifiers, settings.ClipboardPasteHotkeyKey) = v["ClipboardPaste"];
        (settings.ClipboardPasteAsFileHotkeyModifiers, settings.ClipboardPasteAsFileHotkeyKey) = v["ClipboardPasteAsFile"];
        (settings.ClipboardPasteJsonHotkeyModifiers, settings.ClipboardPasteJsonHotkeyKey) = v["ClipboardPasteJson"];
        (settings.ClipboardEditTextHotkeyModifiers, settings.ClipboardEditTextHotkeyKey) = v["ClipboardEditText"];
        (settings.ClipboardShortcutPhraseHotkeyModifiers, settings.ClipboardShortcutPhraseHotkeyKey) = v["ClipboardShortcutPhrase"];
        (settings.ClipboardDeleteHotkeyModifiers, settings.ClipboardDeleteHotkeyKey) = v["ClipboardDelete"];
        (settings.FileJumpFavoriteHotkeyModifiers, settings.FileJumpFavoriteHotkeyKey) = v["FileJumpFavorite"];
        (settings.FileJumpEditPhraseHotkeyModifiers, settings.FileJumpEditPhraseHotkeyKey) = v["FileJumpEditPhrase"];
        (settings.FileJumpRemoveRecentHotkeyModifiers, settings.FileJumpRemoveRecentHotkeyKey) = v["FileJumpRemoveRecent"];
    }
}
