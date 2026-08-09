using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Brush = System.Windows.Media.Brush;
using Button = System.Windows.Controls.Button;
using Orientation = System.Windows.Controls.Orientation;
using TextBox = System.Windows.Controls.TextBox;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using ClipboardManager.Models;

namespace ClipboardManager;

public partial class PopupWindow : Window
{
    private void TryRefreshPickerForNewExternalFolder(IntPtr foregroundHwnd)
    {
        if (_activeFileJumpPicker == null) return;
        if (_appSettings == null) return;

        var dialogHwnd = _activeFileJumpPicker.OwnerDialogHwnd;
        if (dialogHwnd == IntPtr.Zero || !Win32.IsWindow(dialogHwnd)) return;

        var mem = _appSettings.LastFileDialogFolder?.Trim();
        var recentCapture = CopyRecentForJump(_appSettings);

        unchecked { _fileJumpAutoSyncCollectGen++; }
        var gen = _fileJumpAutoSyncCollectGen;
        var dialogCapture = dialogHwnd;

        var th = new Thread(() =>
        {
            List<FileJumpCandidate> candidates;
            try
            {
                candidates = FileManagerPathCollector.CollectCandidates(dialogCapture, mem,
                    recentFolders: recentCapture);
            }
            catch { return; }

            Dispatcher.BeginInvoke(() =>
            {
                if (gen != _fileJumpAutoSyncCollectGen) return;
                TryJumpFileDialogRefreshPickerIfOpen(dialogCapture, candidates);
            }, DispatcherPriority.Normal);
        })
        {
            IsBackground = true,
            Name = "ClipboardX-FileJump-RefreshOnExternal",
        };
        th.SetApartmentState(ApartmentState.STA);
        th.Start();

        // 启动轮询：检测 Explorer 窗口内导航导致的路径变化
        StartExplorerPathPoll(foregroundHwnd);
    }

    /// <summary>Picker 打开时，定时轮询指定 Explorer 窗口的路径变化并刷新列表。</summary>
    private void StartExplorerPathPoll(IntPtr explorerHwnd)
    {
        StopExplorerPathPoll();
        if (explorerHwnd == IntPtr.Zero || !Win32.IsWindow(explorerHwnd)) return;
        if (_activeFileJumpPicker == null) return;

        _explorerPathPollHwnd = explorerHwnd;
        _explorerPathPollLastPath = FileManagerPathCollector.TryGetFolderForWindow(explorerHwnd, fresh: true) ?? "";

        _explorerPathPollTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(600)
        };
        _explorerPathPollTimer.Tick += ExplorerPathPollTick;
        _explorerPathPollTimer.Start();
    }

    private void StopExplorerPathPoll()
    {
        if (_explorerPathPollTimer != null)
        {
            _explorerPathPollTimer.Stop();
            _explorerPathPollTimer.Tick -= ExplorerPathPollTick;
            _explorerPathPollTimer = null;
        }
        _explorerPathPollLastPath = "";
        _explorerPathPollHwnd = IntPtr.Zero;
    }

    private void ExplorerPathPollTick(object? sender, EventArgs e)
    {
        if (_activeFileJumpPicker == null || _explorerPathPollHwnd == IntPtr.Zero)
        {
            StopExplorerPathPoll();
            return;
        }
        if (!Win32.IsWindow(_explorerPathPollHwnd))
        {
            StopExplorerPathPoll();
            return;
        }
        // 前台已不在该 Explorer 窗口，停止轮询
        var fg = Win32.GetForegroundWindow();
        if (fg != _explorerPathPollHwnd)
        {
            StopExplorerPathPoll();
            return;
        }

        var currentPath = FileManagerPathCollector.TryGetFolderForWindow(_explorerPathPollHwnd, fresh: true) ?? "";
        if (string.Equals(currentPath, _explorerPathPollLastPath, StringComparison.OrdinalIgnoreCase))
            return;

        // 路径变化了，刷新 picker 列表
        _explorerPathPollLastPath = currentPath;
        TryRefreshPickerForNewExternalFolder(_explorerPathPollHwnd);
    }

    /// <summary>
    /// 当前键盘焦点是否落在该文件对话框根窗口内（含微信主窗前台 + <see cref="Win32.GetLastActivePopup"/> 模态框等情形）。
    /// </summary>
    private static bool IsForegroundFocusOnFileDialogRoot(IntPtr dialogRoot)
    {
        if (dialogRoot == IntPtr.Zero) return false;
        var fg = Win32.GetForegroundWindow();
        if (fg == IntPtr.Zero) return false;
        var dlg = FileDialogJumpHelper.ResolveFileDialogHwndFromWindowOrAncestor(fg);
        var fgRoot = dlg != IntPtr.Zero
            ? Win32.GetAncestor(dlg, Win32.GA_ROOT)
            : Win32.GetAncestor(fg, Win32.GA_ROOT);
        return fgRoot == dialogRoot;
    }

    /// <summary>
    /// 检测到打开/保存对话框成为前台时，延时后再尝试自动弹出（等对话框内路径可读）。
    /// </summary>
}
