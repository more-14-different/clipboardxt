using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;

namespace ClipboardManager;

public partial class PopupWindow : Window
{
    private void InstallFileJumpAutoMouseHook()
    {
        if (_fileJumpAutoMouseHook != IntPtr.Zero) return;
        s_fileJumpAutoMouseOwner = this;
        ClipboardManager.Services.GlobalHookDispatcher.Dispatcher.Invoke(() =>
        {
            _fileJumpAutoMouseHook = Win32.SetWindowsHookEx(
                Win32.WH_MOUSE_LL, s_fileJumpAutoMouseThunk, Win32.GetModuleHandle(null), 0);
            s_fileJumpAutoMouseHookForNext = _fileJumpAutoMouseHook;
        });
    }

    private IntPtr FileJumpAutoMouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0
            && _fileJumpAutoArmedRoot != IntPtr.Zero
            && _appSettings != null
            && _appSettings.FileJumpAutoOnFirstClick
            && wParam.ToInt32() == Win32.WM_LBUTTONDOWN)
        {
            var info = Marshal.PtrToStructure<Win32.MSLLHOOKSTRUCT>(lParam);
            var clickHwnd = Win32.WindowFromPoint(info.pt);
            if (clickHwnd != IntPtr.Zero
                && Win32.GetAncestor(clickHwnd, Win32.GA_ROOT) == _fileJumpAutoArmedRoot)
            {
                Dispatcher.BeginInvoke(new Action(TryFileJumpAutoNavigateAfterClick),
                    System.Windows.Threading.DispatcherPriority.Normal);
            }
        }
        return Win32.CallNextHookEx(_fileJumpAutoMouseHook, nCode, wParam, lParam);
    }
}
