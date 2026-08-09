using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace ClipboardManager;

public partial class SettingsWindow : Window
{
    /// <summary>
    /// 录制全局快捷键时的修饰键掩码。WPF 的 <see cref="Keyboard.Modifiers"/> 在按住 Win 时常为 None，
    /// 需用 <see cref="Win32.GetAsyncKeyState"/> 检测左右 Win。
    /// </summary>
    private static uint GetHotkeyModifiersForRecording()
    {
        var wpf = Keyboard.Modifiers;
        uint mod = 0;
        if (wpf.HasFlag(ModifierKeys.Control)) mod |= Win32.MOD_CONTROL;
        if (wpf.HasFlag(ModifierKeys.Shift)) mod |= Win32.MOD_SHIFT;
        if (wpf.HasFlag(ModifierKeys.Alt)) mod |= Win32.MOD_ALT;
        if (wpf.HasFlag(ModifierKeys.Windows)) mod |= Win32.MOD_WIN;
        if ((Win32.GetAsyncKeyState(Win32.VK_LWIN) & 0x8000) != 0
            || (Win32.GetAsyncKeyState(Win32.VK_RWIN) & 0x8000) != 0)
            mod |= Win32.MOD_WIN;
        return mod;
    }

    private void BeginHotkeyRecording(
        ref bool isRecording,
        TextBlock displayText,
        UIElement focusTarget,
        string prompt)
    {
        isRecording = true;
        displayText.Text = prompt;
        displayText.Foreground = (System.Windows.Media.Brush)FindResource("AccentBg");
        focusTarget.Focus();
    }

    private void HandleHotkeyRecordingKeyDown(
        ref bool isRecording,
        KeyEventArgs e,
        Action<uint, uint> apply,
        TextBlock displayText)
    {
        if (!isRecording) return;
        e.Handled = true;

        if (!HotkeyRecordingController.TryRecord(e.Key, e.SystemKey, GetHotkeyModifiersForRecording(), out var result))
            return;

        apply(result.Modifiers, result.Key);
        isRecording = false;
        displayText.Text = AppSettings.FormatHotkey(result.Modifiers, result.Key);
        displayText.Foreground = (System.Windows.Media.Brush)FindResource("PrimaryText");
    }

    private void CancelHotkeyRecording(ref bool isRecording, TextBlock displayText, uint modifiers, uint key)
    {
        if (!isRecording) return;

        isRecording = false;
        displayText.Text = AppSettings.FormatHotkey(modifiers, key);
        displayText.Foreground = (System.Windows.Media.Brush)FindResource("PrimaryText");
    }

    private void HotkeyBox_Click(object sender, RoutedEventArgs e)
    {
        BeginHotkeyRecording(ref _isRecordingHotkey, HotkeyText, HotkeyBox, "按下快捷键…");
    }

    private void HotkeyBox_KeyDown(object sender, KeyEventArgs e)
    {
        HandleHotkeyRecordingKeyDown(
            ref _isRecordingHotkey,
            e,
            (modifiers, key) => { _pendingModifiers = modifiers; _pendingKey = key; },
            HotkeyText);
    }

    private void HotkeyBox_LostFocus(object sender, RoutedEventArgs e)
    {
        CancelHotkeyRecording(ref _isRecordingHotkey, HotkeyText, _pendingModifiers, _pendingKey);
    }

    private void FileJumpHotkeyBox_Click(object sender, RoutedEventArgs e)
    {
        BeginHotkeyRecording(ref _isRecordingFileJumpHotkey, FileJumpHotkeyText, FileJumpHotkeyBox, "按下快捷键…");
    }

    private void FileJumpHotkeyBox_KeyDown(object sender, KeyEventArgs e)
    {
        HandleHotkeyRecordingKeyDown(
            ref _isRecordingFileJumpHotkey,
            e,
            (modifiers, key) => { _pendingFileJumpModifiers = modifiers; _pendingFileJumpKey = key; },
            FileJumpHotkeyText);
    }

    private void FileJumpHotkeyBox_LostFocus(object sender, RoutedEventArgs e)
    {
        CancelHotkeyRecording(ref _isRecordingFileJumpHotkey, FileJumpHotkeyText, _pendingFileJumpModifiers, _pendingFileJumpKey);
    }

    private void BatchModeCycleHotkeyBox_Click(object sender, RoutedEventArgs e)
    {
        BeginHotkeyRecording(ref _isRecordingBatchModeCycleHotkey, BatchModeCycleHotkeyText, BatchModeCycleHotkeyBox, "按下快捷键…");
    }

    private void BatchModeCycleHotkeyBox_KeyDown(object sender, KeyEventArgs e)
    {
        HandleHotkeyRecordingKeyDown(
            ref _isRecordingBatchModeCycleHotkey,
            e,
            (modifiers, key) => { _pendingBatchModeCycleModifiers = modifiers; _pendingBatchModeCycleKey = key; },
            BatchModeCycleHotkeyText);
    }

    private void BatchModeCycleHotkeyBox_LostFocus(object sender, RoutedEventArgs e)
    {
        CancelHotkeyRecording(ref _isRecordingBatchModeCycleHotkey, BatchModeCycleHotkeyText, _pendingBatchModeCycleModifiers, _pendingBatchModeCycleKey);
    }

    private void PanelPageUpKeyBox_Click(object sender, RoutedEventArgs e)
    {
        BeginHotkeyRecording(ref _isRecordingPageScrollUpHotkey, PanelPageUpKeyText, PanelPageUpKeyBox, "按下组合键…");
    }

    private void PanelPageUpKeyBox_KeyDown(object sender, KeyEventArgs e)
    {
        HandleHotkeyRecordingKeyDown(
            ref _isRecordingPageScrollUpHotkey,
            e,
            (modifiers, key) => { _pendingPageScrollUpModifiers = modifiers; _pendingPageScrollUpKey = key; },
            PanelPageUpKeyText);
    }

    private void PanelPageUpKeyBox_LostFocus(object sender, RoutedEventArgs e)
    {
        CancelHotkeyRecording(ref _isRecordingPageScrollUpHotkey, PanelPageUpKeyText, _pendingPageScrollUpModifiers, _pendingPageScrollUpKey);
    }

    private void PanelPageDownKeyBox_Click(object sender, RoutedEventArgs e)
    {
        BeginHotkeyRecording(ref _isRecordingPageScrollDownHotkey, PanelPageDownKeyText, PanelPageDownKeyBox, "按下组合键…");
    }

    private void PanelPageDownKeyBox_KeyDown(object sender, KeyEventArgs e)
    {
        HandleHotkeyRecordingKeyDown(
            ref _isRecordingPageScrollDownHotkey,
            e,
            (modifiers, key) => { _pendingPageScrollDownModifiers = modifiers; _pendingPageScrollDownKey = key; },
            PanelPageDownKeyText);
    }

    private void PanelPageDownKeyBox_LostFocus(object sender, RoutedEventArgs e)
    {
        CancelHotkeyRecording(ref _isRecordingPageScrollDownHotkey, PanelPageDownKeyText, _pendingPageScrollDownModifiers, _pendingPageScrollDownKey);
    }

    private void StarToggleHotkeyBox_Click(object sender, RoutedEventArgs e)
    {
        BeginHotkeyRecording(ref _isRecordingStarToggleHotkey, StarToggleHotkeyText, StarToggleHotkeyBox, "按下组合键…");
    }

    private void StarToggleHotkeyBox_KeyDown(object sender, KeyEventArgs e)
    {
        HandleHotkeyRecordingKeyDown(
            ref _isRecordingStarToggleHotkey,
            e,
            (modifiers, key) => { _pendingStarToggleModifiers = modifiers; _pendingStarToggleKey = key; },
            StarToggleHotkeyText);
    }

    private void StarToggleHotkeyBox_LostFocus(object sender, RoutedEventArgs e)
    {
        CancelHotkeyRecording(ref _isRecordingStarToggleHotkey, StarToggleHotkeyText, _pendingStarToggleModifiers, _pendingStarToggleKey);
    }
}

