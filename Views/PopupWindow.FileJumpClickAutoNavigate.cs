using System;
using System.Windows;
using ClipboardManager.Models;

namespace ClipboardManager;

public partial class PopupWindow : Window
{
    private void DisarmFileJumpClickToNavigate()
    {
        _fileJumpAutoArmedDialog = IntPtr.Zero;
        _fileJumpAutoArmedRoot = IntPtr.Zero;
        if (_fileJumpAutoMouseHook != IntPtr.Zero)
        {
            var hk = _fileJumpAutoMouseHook;
            _fileJumpAutoMouseHook = IntPtr.Zero;
            ClipboardManager.Services.GlobalHookDispatcher.Dispatcher.Invoke(() => Win32.UnhookWindowsHookEx(hk));
        }
        if (s_fileJumpAutoMouseOwner == this)
        {
            s_fileJumpAutoMouseOwner = null;
            s_fileJumpAutoMouseHookForNext = IntPtr.Zero;
        }
    }

    /// <summary>
    /// 对话框成为前台后由 <see cref="OnForegroundChanged"/> 调度；在满足条件时挂接低级鼠标钩，等待首次落在对话框内的左键。
    /// 每个对话框顶层窗口在存活期内仅自动跳转成功一次（关闭后再开视为新窗口）。
    /// </summary>
    private void UpdateFileJumpClickToNavigateArm(IntPtr foregroundHwnd)
    {
        if (_appSettings == null || !_appSettings.FileJumpAutoOnFirstClick)
        {
            DisarmFileJumpClickToNavigate();
            return;
        }

        // 「对话框打开时自动弹出列表」开启时，前台事件已经会触发自动弹列表/直跳，不需要再装低级鼠标钩兜底；
        // 装钩会让全局鼠标消息绕一次 UI 线程，体感卡顿。仅在纯「自动跳转到最佳路径」+ 弹列表关闭时才需要兜底。
        if (_appSettings.FileJumpPickerOpenWhenDialogForeground)
        {
            DisarmFileJumpClickToNavigate();
            return;
        }

        if (_fileJumpAutoFirstJumpDoneRoot != IntPtr.Zero
            && !Win32.IsWindow(_fileJumpAutoFirstJumpDoneRoot))
            _fileJumpAutoFirstJumpDoneRoot = IntPtr.Zero;

        if (_activeFileJumpPicker != null || _fileJumpPickerOpenInProgress)
        {
            DisarmFileJumpClickToNavigate();
            return;
        }

        if (foregroundHwnd == _hwnd)
        {
            DisarmFileJumpClickToNavigate();
            return;
        }

        var dialogHwnd = FileDialogJumpHelper.ResolveFileDialogHwndFromWindowOrAncestor(foregroundHwnd);
        if (dialogHwnd == IntPtr.Zero)
        {
            if (_fileJumpAutoArmedRoot != IntPtr.Zero && Win32.IsWindow(foregroundHwnd))
            {
                var fgRoot = Win32.GetAncestor(foregroundHwnd, Win32.GA_ROOT);
                if (fgRoot == _fileJumpAutoArmedRoot)
                    return;
            }
            DisarmFileJumpClickToNavigate();
            return;
        }
        var dialogRoot = Win32.GetAncestor(dialogHwnd, Win32.GA_ROOT);
        if (IsFileJumpNavigationSuppressed(dialogHwnd))
        {
            DisarmFileJumpClickToNavigate();
            return;
        }
        if (dialogRoot != IntPtr.Zero
            && dialogRoot == _fileJumpAutoFirstJumpDoneRoot
            && Win32.IsWindow(dialogRoot))
        {
            DisarmFileJumpClickToNavigate();
            return;
        }

        if (_fileJumpAutoArmedDialog == dialogHwnd && _fileJumpAutoMouseHook != IntPtr.Zero)
            return;

        _fileJumpAutoArmedDialog = dialogHwnd;
        _fileJumpAutoArmedRoot = dialogRoot;
        InstallFileJumpAutoMouseHook();
    }

    private void TryFileJumpAutoNavigateAfterClick()
    {
        try
        {
            if (_appSettings == null) return;
            var dlg = _fileJumpAutoArmedDialog;
            if (dlg == IntPtr.Zero || !Win32.IsWindow(dlg)) return;
            if (FileDialogJumpHelper.ClassifyFileDialog(dlg) == FileDialogKind.None
                && CustomFileDialogStore.FindMatchingRule(dlg) == null) return;

            var fg = Win32.GetForegroundWindow();
            var fgDlg = FileDialogJumpHelper.ResolveFileDialogHwndFromWindowOrAncestor(fg);
            if (fgDlg == IntPtr.Zero || Win32.GetAncestor(fgDlg, Win32.GA_ROOT) != _fileJumpAutoArmedRoot)
                return;

            var mem = _appSettings.LastFileDialogFolder?.Trim();
            var recentCapture = CopyRecentForJump(_appSettings);
            var candidates = FileManagerPathCollector.CollectCandidates(dlg, mem,
                recentFolders: recentCapture);
            if (candidates.Count == 0) return;

            var doneRoot = Win32.GetAncestor(dlg, Win32.GA_ROOT);
            DisarmFileJumpClickToNavigate();
            NavigateToFolderInBackground(dlg, candidates[0].Path, _appSettings.EnableShellNavigateInject,
                ok =>
                {
                    if (ok && doneRoot != IntPtr.Zero)
                        _fileJumpAutoFirstJumpDoneRoot = doneRoot;
                });
        }
        catch (Exception ex)
        {
            ShellNavigateLog.Write("filejump", "TryFileJumpAutoNavigateAfterClick: " + ex);
            DisarmFileJumpClickToNavigate();
        }
    }
}
