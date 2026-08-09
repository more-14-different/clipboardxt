using System.Windows;
using System.Windows.Input;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace ClipboardManager;

public partial class SettingsWindow
{
    private void ImageOcrEnabled_Click(object sender, RoutedEventArgs e)
    {
        _pendingImageOcrEnabled = !_pendingImageOcrEnabled;
        ImageOcrEnabledText.Text = _pendingImageOcrEnabled ? "开启" : "关闭";
    }

    private static uint GetPassthroughModifiersForRecording()
    {
        var modifiers = GetHotkeyModifiersForRecording();
        if ((Win32.GetAsyncKeyState((int)Win32.VK_CAPITAL) & 0x8000) != 0)
            modifiers |= Win32.MOD_CAPS;
        return modifiers;
    }

    private void ApplyKeyPassthroughModifierChecksFromMask()
    {
        KeyPassthroughCapsCheck.IsChecked = (_pendingKeyPassthroughModifierMask & Win32.MOD_CAPS) != 0;
        KeyPassthroughShiftCheck.IsChecked = (_pendingKeyPassthroughModifierMask & Win32.MOD_SHIFT) != 0;
        KeyPassthroughCtrlCheck.IsChecked = (_pendingKeyPassthroughModifierMask & Win32.MOD_CONTROL) != 0;
        KeyPassthroughAltCheck.IsChecked = (_pendingKeyPassthroughModifierMask & Win32.MOD_ALT) != 0;
        KeyPassthroughWinCheck.IsChecked = (_pendingKeyPassthroughModifierMask & Win32.MOD_WIN) != 0;
    }

    private void RebuildKeyPassthroughModifierMaskFromChecks()
    {
        uint mask = 0;
        if (KeyPassthroughCapsCheck.IsChecked == true) mask |= Win32.MOD_CAPS;
        if (KeyPassthroughShiftCheck.IsChecked == true) mask |= Win32.MOD_SHIFT;
        if (KeyPassthroughCtrlCheck.IsChecked == true) mask |= Win32.MOD_CONTROL;
        if (KeyPassthroughAltCheck.IsChecked == true) mask |= Win32.MOD_ALT;
        if (KeyPassthroughWinCheck.IsChecked == true) mask |= Win32.MOD_WIN;
        _pendingKeyPassthroughModifierMask = mask;
    }

    private void ReloadKeyPassthroughRulesList()
    {
        KeyPassthroughRulesList.Items.Clear();
        foreach (var rule in _pendingKeyPassthroughRules)
            KeyPassthroughRulesList.Items.Add(rule);
    }

    private void KeyPassthroughEnabled_Click(object sender, RoutedEventArgs e)
    {
        _pendingKeyPassthroughEnabled = !_pendingKeyPassthroughEnabled;
        KeyPassthroughEnabledText.Text = _pendingKeyPassthroughEnabled ? "开启" : "关闭";
    }

    private void KeyPassthroughModifierCheck_Changed(object sender, RoutedEventArgs e) =>
        RebuildKeyPassthroughModifierMaskFromChecks();

    private void KeyPassthroughRecordBox_Click(object sender, RoutedEventArgs e)
    {
        _isRecordingKeyPassthroughRule = true;
        KeyPassthroughRecordText.Text = "按下组合键…";
        KeyPassthroughRecordBox.Focus();
    }

    private void KeyPassthroughRecordBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (!_isRecordingKeyPassthroughRule) return;
        e.Handled = true;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftShift or Key.RightShift
            or Key.LeftAlt or Key.RightAlt or Key.LWin or Key.RWin or Key.CapsLock)
            return;

        var modifiers = GetPassthroughModifiersForRecording();
        if (modifiers == 0) return;
        var rule = new KeyPassthroughRule
        {
            Modifiers = modifiers,
            Key = (uint)KeyInterop.VirtualKeyFromKey(key)
        };
        _isRecordingKeyPassthroughRule = false;
        if (!_pendingKeyPassthroughRules.Any(r => r.Modifiers == rule.Modifiers && r.Key == rule.Key))
            _pendingKeyPassthroughRules.Add(rule);
        ReloadKeyPassthroughRulesList();
        KeyPassthroughRecordText.Text = "录制组合键…";
    }

    private void KeyPassthroughRecordBox_LostFocus(object sender, RoutedEventArgs e)
    {
        _isRecordingKeyPassthroughRule = false;
        KeyPassthroughRecordText.Text = "录制组合键…";
    }

    private void KeyPassthroughRuleDelete_Click(object sender, RoutedEventArgs e)
    {
        if (KeyPassthroughRulesList.SelectedItem is not KeyPassthroughRule selected) return;
        _pendingKeyPassthroughRules.RemoveAll(r => r.Modifiers == selected.Modifiers && r.Key == selected.Key);
        ReloadKeyPassthroughRulesList();
    }
}
