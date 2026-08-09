using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;

namespace ClipboardManager;

public partial class PopupWindow : Window
{
    private static IntPtr StaticMouseHookProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        var owner = s_popupMouseHookOwner;
        var hhk = s_popupMouseHookForNext;
        if (owner != null && hhk != IntPtr.Zero)
        {
            try { return owner.MouseHookCallback(nCode, wParam, lParam); }
            catch (Exception ex) { ClipboardDiagnosticsLog.Write($"native popup mouse hook exception: {ex}"); }
        }
        return Win32.CallNextHookEx(hhk, nCode, wParam, lParam);
    }

    private static IntPtr StaticFileJumpAutoMouseProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        var owner = s_fileJumpAutoMouseOwner;
        var hhk = s_fileJumpAutoMouseHookForNext;
        if (owner != null && hhk != IntPtr.Zero)
        {
            try { return owner.FileJumpAutoMouseHookCallback(nCode, wParam, lParam); }
            catch (Exception ex) { ClipboardDiagnosticsLog.Write($"native file-jump auto mouse hook exception: {ex}"); }
        }
        return Win32.CallNextHookEx(hhk, nCode, wParam, lParam);
    }

#if CLIPX_FILEJUMP
    private static IntPtr StaticFileJumpPersistMouseProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        var owner = s_fileJumpPersistMouseOwner;
        var hhk = s_fileJumpPersistMouseHookForNext;
        if (owner != null && hhk != IntPtr.Zero)
        {
            try { return owner.FileJumpPersistMouseHookCallback(nCode, wParam, lParam); }
            catch (Exception ex) { ClipboardDiagnosticsLog.Write($"native file-jump persist mouse hook exception: {ex}"); }
        }
        return Win32.CallNextHookEx(hhk, nCode, wParam, lParam);
    }
#endif

    private void InstallMouseHook()
    {
        if (_mouseHook != IntPtr.Zero) return;
        s_popupMouseHookOwner = this;
        ClipboardManager.Services.GlobalHookDispatcher.Dispatcher.Invoke(() =>
        {
            _mouseHook = Win32.SetWindowsHookEx(
                Win32.WH_MOUSE_LL, s_popupMouseHookThunk, Win32.GetModuleHandle(null), 0);
            s_popupMouseHookForNext = _mouseHook;
        });
    }

    private void UninstallMouseHook()
    {
        if (_mouseHook != IntPtr.Zero)
        {
            var hk = _mouseHook;
            _mouseHook = IntPtr.Zero;
            ClipboardManager.Services.GlobalHookDispatcher.Dispatcher.Invoke(() => Win32.UnhookWindowsHookEx(hk));
        }
        if (s_popupMouseHookOwner == this)
        {
            s_popupMouseHookOwner = null;
            s_popupMouseHookForNext = IntPtr.Zero;
        }
    }
}
