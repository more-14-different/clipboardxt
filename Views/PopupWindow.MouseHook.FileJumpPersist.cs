using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;

namespace ClipboardManager;

public partial class PopupWindow : Window
{
#if CLIPX_FILEJUMP
    private void InstallFileJumpPersistFolderHook()
    {
        if (_fileJumpPersistMouseHook != IntPtr.Zero) return;
        s_fileJumpPersistMouseOwner = this;
        ClipboardManager.Services.GlobalHookDispatcher.Dispatcher.Invoke(() =>
        {
            _fileJumpPersistMouseHook = Win32.SetWindowsHookEx(
                Win32.WH_MOUSE_LL, s_fileJumpPersistMouseThunk, Win32.GetModuleHandle(null), 0);
            s_fileJumpPersistMouseHookForNext = _fileJumpPersistMouseHook;
        });
    }

    private void UninstallFileJumpPersistFolderHook()
    {
        if (_fileJumpPersistMouseHook != IntPtr.Zero)
        {
            var hk = _fileJumpPersistMouseHook;
            _fileJumpPersistMouseHook = IntPtr.Zero;
            ClipboardManager.Services.GlobalHookDispatcher.Dispatcher.Invoke(() => Win32.UnhookWindowsHookEx(hk));
        }

        if (s_fileJumpPersistMouseOwner == this)
        {
            s_fileJumpPersistMouseOwner = null;
            s_fileJumpPersistMouseHookForNext = IntPtr.Zero;
        }
    }

    private IntPtr FileJumpPersistMouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0
            && _appSettings != null
            && wParam.ToInt32() == Win32.WM_LBUTTONDOWN)
        {
            var sinceLastDialog = Environment.TickCount64 - _lastFileDialogSeenTick;
            if (sinceLastDialog < FileDialogAliveWindowMs && sinceLastDialog >= 0)
            {
                var info = Marshal.PtrToStructure<Win32.MSLLHOOKSTRUCT>(lParam);
                if (FileDialogConfirmClick.TryResolveDialogOnPrimaryConfirmClick(info.pt, out var dialogHwnd))
                {
                    var dlgCapture = dialogHwnd;
                    Dispatcher.BeginInvoke(new Action(() => TryPersistRecentFolderAfterPrimaryClick(dlgCapture)),
                        DispatcherPriority.Send);
                }
            }
        }

        return Win32.CallNextHookEx(_fileJumpPersistMouseHook, nCode, wParam, lParam);
    }

    private void TryPersistRecentFolderAfterPrimaryClick(IntPtr dialogHwnd)
    {
        try
        {
            if (_appSettings == null) return;
            if (dialogHwnd == IntPtr.Zero || !Win32.IsWindow(dialogHwnd)) return;
            // DLL 注入读取路径较慢，放到后台线程避免阻塞 UI
            var capturedDlg = dialogHwnd;
            var th = new Thread(() =>
            {
                if (!FileDialogJumpHelper.TryReadCurrentFolder(capturedDlg, out var folder)
                    || string.IsNullOrEmpty(folder)) return;
                Dispatcher.BeginInvoke(() => RememberLastDialogFolder(folder), DispatcherPriority.Background);
            }) { IsBackground = true, Name = "ClipboardX-PersistFolder" };
            th.Start();
        }
        catch (Exception ex)
        {
            ShellNavigateLog.Write("filejump", "TryPersistRecentFolderAfterPrimaryClick: " + ex);
        }
    }
#endif
}
