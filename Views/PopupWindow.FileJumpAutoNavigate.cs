using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using ClipboardManager.Models;

namespace ClipboardManager;

public partial class PopupWindow : Window
{
    /// <summary>
    /// 「自动跳转到最佳路径」开 + 「自动弹列表」关：对话框到前台后采集候选并直跳首条，不弹列表。
    /// 同一对话框 root 仅成功一次。配合 <see cref="UpdateFileJumpClickToNavigateArm"/> 的鼠标钩兜底。
    /// </summary>
    private void TryAutoNavigateBestPathWhenDialogForeground(IntPtr foregroundHwnd)
    {
        if (_appSettings == null) return;
        if (_appSettings.FileJumpPickerOpenWhenDialogForeground) return; // 该路径仅用于纯直跳
        if (!_appSettings.FileJumpAutoOnFirstClick) return;
        if (foregroundHwnd == IntPtr.Zero) return;

        if (_fileJumpAutoFirstJumpDoneRoot != IntPtr.Zero
            && !Win32.IsWindow(_fileJumpAutoFirstJumpDoneRoot))
            _fileJumpAutoFirstJumpDoneRoot = IntPtr.Zero;

        var fgNow = Win32.GetForegroundWindow();
        var dialogHwnd = ResolveFileJumpTargetHwndInternal(fgNow);
        if (dialogHwnd == IntPtr.Zero) return;
        if (IsFileJumpNavigationSuppressed(dialogHwnd)) return;

        var dialogRoot = Win32.GetAncestor(dialogHwnd, Win32.GA_ROOT);
        if (dialogRoot == IntPtr.Zero) return;
        if (dialogRoot == _fileJumpAutoFirstJumpDoneRoot) return;
        if (!IsForegroundFocusOnFileDialogRoot(dialogRoot)) return;

        var mem = _appSettings.LastFileDialogFolder?.Trim();
        var allowShellInject = _appSettings.EnableShellNavigateInject;
        var recentCapture = CopyRecentForJump(_appSettings);
        var dialogHwndCapture = dialogHwnd;
        var dialogRootCapture = dialogRoot;

        void StaCollect()
        {
            List<FileJumpCandidate> candidates;
            try
            {
                candidates = FileManagerPathCollector.CollectCandidates(dialogHwndCapture, mem,
                    recentFolders: recentCapture);
            }
            catch (Exception ex)
            {
                ShellNavigateLog.Write("filejump", "CollectCandidates (auto navigate fg): " + ex);
                candidates = new List<FileJumpCandidate>();
            }

            if (candidates.Count == 0) return;

            Dispatcher.BeginInvoke(() =>
            {
                if (_appSettings == null) return;
                if (_appSettings.FileJumpPickerOpenWhenDialogForeground) return;
                if (!_appSettings.FileJumpAutoOnFirstClick) return;
                if (!Win32.IsWindow(dialogHwndCapture)) return;
                var rootNow = Win32.GetAncestor(dialogHwndCapture, Win32.GA_ROOT);
                if (rootNow == IntPtr.Zero || rootNow != dialogRootCapture) return;
                if (rootNow == _fileJumpAutoFirstJumpDoneRoot) return;
                if (!IsForegroundFocusOnFileDialogRoot(rootNow)) return;

                var prefer = PreferCandidateIndex(dialogHwndCapture, candidates);
                var capturedRoot = rootNow;
                NavigateToFolderInBackground(dialogHwndCapture, candidates[prefer].Path, allowShellInject,
                    ok =>
                    {
                        if (ok)
                        {
                            _fileJumpAutoFirstJumpDoneRoot = capturedRoot;
                            DisarmFileJumpClickToNavigate();
                        }
                    });
            }, DispatcherPriority.Normal);
        }

        var th = new Thread(StaCollect)
        {
            IsBackground = true,
            Name = "ClipboardX-FileJump-AutoNav-Collect",
        };
        th.SetApartmentState(ApartmentState.STA);
        th.Start();
    }
}
