using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace ClipboardManager;

public partial class SettingsWindow : Window
{    private void ShellInjectCycle_Click(object sender, RoutedEventArgs e)
    {
        _pendingEnableShellNavigateInject = !_pendingEnableShellNavigateInject;
        ShellInjectText.Text = _pendingEnableShellNavigateInject ? "开启" : "关闭";
    }

    private void FileJumpFollowCycle_Click(object sender, RoutedEventArgs e)
    {
        _pendingFileJumpFollowMode = FileJumpPickerFollowModes.IsDialog(_pendingFileJumpFollowMode)
            ? FileJumpPickerFollowModes.Mouse
            : FileJumpPickerFollowModes.Dialog;
        FileJumpFollowText.Text = FileJumpPickerFollowModes.IsDialog(_pendingFileJumpFollowMode) ? "跟随对话框" : "跟随鼠标";
    }

    private void FileJumpOpenListOnDialogOpenCycle_Click(object sender, RoutedEventArgs e)
    {
        _pendingFileJumpOpenListOnDialogOpen = !_pendingFileJumpOpenListOnDialogOpen;
        FileJumpOpenListOnDialogOpenText.Text = _pendingFileJumpOpenListOnDialogOpen ? "开启" : "关闭";
        UpdateFileJumpFollowVisibility();
    }

    private void FileJumpAutoNavigateBestCycle_Click(object sender, RoutedEventArgs e)
    {
        _pendingFileJumpAutoNavigateBest = !_pendingFileJumpAutoNavigateBest;
        FileJumpAutoNavigateBestText.Text = _pendingFileJumpAutoNavigateBest ? "开启" : "关闭";
    }

    private void UpdateFileJumpFollowVisibility()
    {
        var vis = _pendingFileJumpOpenListOnDialogOpen ? Visibility.Collapsed : Visibility.Visible;
        FileJumpFollowLabel.Visibility = vis;
        FileJumpFollowBox.Visibility = vis;
    }

    private void FileJumpAutoSyncCycle_Click(object sender, RoutedEventArgs e)
    {
        _pendingFileJumpAutoSyncOnReturn = !_pendingFileJumpAutoSyncOnReturn;
        FileJumpAutoSyncText.Text = _pendingFileJumpAutoSyncOnReturn ? "开启" : "关闭";
    }

    private void FileJumpEverythingFolderCycle_Click(object sender, RoutedEventArgs e)
    {
        _pendingFileJumpPickerEverythingFolderSearch = !_pendingFileJumpPickerEverythingFolderSearch;
        FileJumpEverythingFolderText.Text = _pendingFileJumpPickerEverythingFolderSearch ? "开启" : "关闭";
    }

    private void ExplorerEverythingQuickFindCycle_Click(object sender, MouseButtonEventArgs e)
    {
#if CLIPX_FILEJUMP
        _pendingExplorerEverythingQuickFind = !_pendingExplorerEverythingQuickFind;
        ExplorerEverythingQuickFindText.Text = _pendingExplorerEverythingQuickFind ? "开启" : "关闭";
#endif
    }

    private void ExplorerQuickFindOpenModeCycle_Click(object sender, MouseButtonEventArgs e)
    {
#if CLIPX_FILEJUMP
        _pendingExplorerQuickFindOpenMode = _pendingExplorerQuickFindOpenMode == "Explorer" ? "DirectOpen" : "Explorer";
        ExplorerQuickFindOpenModeText.Text = _pendingExplorerQuickFindOpenMode == "DirectOpen" ? "直接打开" : "从资源管理器打开";
#endif
    }
}

