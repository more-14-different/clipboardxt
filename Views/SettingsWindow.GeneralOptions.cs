using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace ClipboardManager;

public partial class SettingsWindow : Window
{
    private void RememberPanelOperationStateCycle_Click(object sender, RoutedEventArgs e)
    {
        _pendingRememberPanelOperationState = !_pendingRememberPanelOperationState;
        RememberPanelOperationStateText.Text = _pendingRememberPanelOperationState ? "开启" : "关闭";
    }

    private static string ThemeDisplayName(string t) => t switch
    {
        "Dark" => "暗色",
        "Light" => "亮色",
        _ => "跟随系统"
    };

    private static string PositionDisplayName(string p) => p switch
    {
        "Mouse" => "鼠标处",
        _ => "光标处"
    };

    private void ThemeCycle_Click(object sender, RoutedEventArgs e)
    {
        _pendingTheme = _pendingTheme switch
        {
            "System" => "Dark",
            "Dark" => "Light",
            _ => "System"
        };
        ThemeText.Text = ThemeDisplayName(_pendingTheme);
        ThemeManager.Apply(_pendingTheme);
    }

    private void PositionCycle_Click(object sender, RoutedEventArgs e)
    {
        _pendingPosition = _pendingPosition == "Caret" ? "Mouse" : "Caret";
        PositionText.Text = PositionDisplayName(_pendingPosition);
    }

    private void ClickHideCycle_Click(object sender, RoutedEventArgs e)
    {
        _pendingHideOnClick = !_pendingHideOnClick;
        ClickHideText.Text = _pendingHideOnClick ? "任意点击隐藏" : "仅切换应用隐藏";
    }

    private void StartupCycle_Click(object sender, RoutedEventArgs e)
    {
        _pendingRunAtStartup = !_pendingRunAtStartup;
        StartupText.Text = _pendingRunAtStartup ? "开启" : "关闭";
    }

    private void RunAsAdminCycle_Click(object sender, RoutedEventArgs e)
    {
        _pendingRunAsAdministrator = !_pendingRunAsAdministrator;
        RunAsAdminText.Text = _pendingRunAsAdministrator ? "开启" : "关闭";
    }

    private void CheckUpdateOnStartupCycle_Click(object sender, RoutedEventArgs e)
    {
        _pendingCheckUpdatesOnStartup = !_pendingCheckUpdatesOnStartup;
        CheckUpdateOnStartupText.Text = _pendingCheckUpdatesOnStartup ? "开启" : "关闭";
    }

    private void ReplaceSystemWinV_Click(object sender, RoutedEventArgs e)
    {
        _pendingReplaceSystemWinV = !_pendingReplaceSystemWinV;
        ReplaceSystemWinVText.Text = _pendingReplaceSystemWinV ? "开启" : "关闭";
    }

    private void ClearHistoryOnExit_Click(object sender, RoutedEventArgs e)
    {
        _pendingClearHistoryOnExit = !_pendingClearHistoryOnExit;
        ClearHistoryOnExitText.Text = _pendingClearHistoryOnExit ? "开启" : "关闭";
    }
}

