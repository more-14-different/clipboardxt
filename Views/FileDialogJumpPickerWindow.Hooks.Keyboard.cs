using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;

namespace ClipboardManager;

public partial class FileDialogJumpPickerWindow : Window
{
    private static IntPtr StaticJumpPickerKeyboardHook(int nCode, IntPtr wParam, IntPtr lParam)
    {
        var owner = s_jumpPickerKbOwner;
        var hhk = s_jumpPickerKbHookForNext;
        if (owner != null && hhk != IntPtr.Zero)
        {
            try { return owner.JumpKeyboardHookProc(nCode, wParam, lParam); }
            catch (Exception ex) { ClipboardDiagnosticsLog.Write($"native jump-picker keyboard hook exception: {ex}"); }
        }
        return Win32.CallNextHookEx(hhk, nCode, wParam, lParam);
    }

    private void InstallKeyboardHook()
    {
        if (_jumpKeyboardHook != IntPtr.Zero) return;
        s_jumpPickerKbOwner = this;
        ClipboardManager.Services.GlobalHookDispatcher.Dispatcher.Invoke(() =>
        {
            _jumpKeyboardHook = Win32.SetWindowsHookEx(
                Win32.WH_KEYBOARD_LL, s_jumpPickerKbThunk, Win32.GetModuleHandle(null), 0);
            s_jumpPickerKbHookForNext = _jumpKeyboardHook;
        });
    }

    private void UninstallKeyboardHook()
    {
        if (_jumpKeyboardHook == IntPtr.Zero) return;
        var hk = _jumpKeyboardHook;
        _jumpKeyboardHook = IntPtr.Zero;
        ClipboardManager.Services.GlobalHookDispatcher.Dispatcher.Invoke(() => Win32.UnhookWindowsHookEx(hk));
        if (s_jumpPickerKbOwner == this)
        {
            s_jumpPickerKbOwner = null;
            s_jumpPickerKbHookForNext = IntPtr.Zero;
        }
    }

    private IntPtr JumpKeyboardHookProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode < 0)
            return Win32.CallNextHookEx(_jumpKeyboardHook, nCode, wParam, lParam);
        if (wParam != (IntPtr)Win32.WM_KEYDOWN)
            return Win32.CallNextHookEx(_jumpKeyboardHook, nCode, wParam, lParam);

        var kb = Marshal.PtrToStructure<Win32.KBDLLHOOKSTRUCT>(lParam);
        if ((kb.flags & (Win32.LLKHF_INJECTED | Win32.LLKHF_LOWER_IL_INJECTED)) != 0)
            return Win32.CallNextHookEx(_jumpKeyboardHook, nCode, wParam, lParam);
        if (ExternalLauncherHotkeyHelper.IsTriggerKey(kb.vkCode))
            return Win32.CallNextHookEx(_jumpKeyboardHook, nCode, wParam, lParam);

        if (_suppressJumpHook || _suppressJumpHookForClipboardPopup)
            return Win32.CallNextHookEx(_jumpKeyboardHook, nCode, wParam, lParam);
        if (!KeyboardHookShouldObserveForeground())
            return Win32.CallNextHookEx(_jumpKeyboardHook, nCode, wParam, lParam);
        if (KeyboardFocusIsExternalEditable())
            return Win32.CallNextHookEx(_jumpKeyboardHook, nCode, wParam, lParam);

        try
        {
            var command = ResolveKeyCommand(kb.vkCode, kb.scanCode);
            if (command.IsHandled)
            {
                Dispatcher.BeginInvoke(() => ApplyKeyCommand(command),
                    DispatcherPriority.Input);
                return (IntPtr)1;
            }
        }
        catch
        {
            // fall through to CallNextHookEx
        }

        return Win32.CallNextHookEx(_jumpKeyboardHook, nCode, wParam, lParam);
    }

    private bool KeyboardHookShouldObserveForeground()
    {
        if (_hwnd == IntPtr.Zero) return false;

        var fg = Win32.GetForegroundWindow();
        if (fg == IntPtr.Zero) return false;
        if (fg == _hwnd) return true;

        // 全局独立模式：面板本身永远不会成为前台（WS_EX_NOACTIVATE），
        // 因此只要触发时的原窗口（或其进程根窗口）仍是前台，就响应键盘。
        if (_isStandaloneMode && _standaloneTargetHwnd != IntPtr.Zero)
        {
            var targetRoot = Win32.GetAncestor(_standaloneTargetHwnd, Win32.GA_ROOT);
            var fgRootForStandalone = Win32.GetAncestor(fg, Win32.GA_ROOT);
            if (targetRoot != IntPtr.Zero && fgRootForStandalone == targetRoot)
                return true;
        }

        var ownerRoot = _fileDialogOwnerHwnd != IntPtr.Zero && Win32.IsWindow(_fileDialogOwnerHwnd)
            ? Win32.GetAncestor(_fileDialogOwnerHwnd, Win32.GA_ROOT)
            : IntPtr.Zero;
        if (ownerRoot == IntPtr.Zero) return false;

        var fgDialog = FileDialogJumpHelper.ResolveFileDialogHwndFromWindowOrAncestor(fg);
        var fgRoot = fgDialog != IntPtr.Zero
            ? Win32.GetAncestor(fgDialog, Win32.GA_ROOT)
            : Win32.GetAncestor(fg, Win32.GA_ROOT);

        return fgRoot != IntPtr.Zero && fgRoot == ownerRoot;
    }

    /// <summary>
    /// 前台线程焦点在「另存为」等系统对话框的文件名编辑框时，不把按键留给跳转列表，
    /// 便于在保持跳转面板打开的同时修改文件名。
    /// </summary>
    private bool KeyboardFocusIsExternalEditable()
    {
        if (_hwnd == IntPtr.Zero) return false;
        IntPtr fg = Win32.GetForegroundWindow();
        if (fg == IntPtr.Zero) return false;

        // 只要面板还开着，不管系统或 Electron 怎么把焦点切到输入框，
        // 我们都强制拦截按键（保持面板的键盘霸权），把敲击送到搜索框里。
        // 如果用户需要输入原生文件对话框，只需按下 Esc 关闭面板即可。
        var resolvedDialog = FileDialogJumpHelper.ResolveFileDialogHwndFromWindowOrAncestor(fg);
        if (resolvedDialog != IntPtr.Zero)
        {
            return false;
        }

        uint tid = Win32.GetWindowThreadProcessId(fg, out _);
        var gti = new Win32.GUITHREADINFO { cbSize = Marshal.SizeOf<Win32.GUITHREADINFO>() };
        if (!Win32.GetGUIThreadInfo(tid, ref gti) || gti.hwndFocus == IntPtr.Zero)
            return false;
        if (IsFocusWithinJumpPicker(gti.hwndFocus))
            return false;
        return IsEditableTextHwnd(gti.hwndFocus);
    }

    private bool IsFocusWithinJumpPicker(IntPtr hwndFocus)
    {
        for (IntPtr h = hwndFocus; h != IntPtr.Zero; h = Win32.GetParent(h))
        {
            if (h == _hwnd) return true;
        }
        return false;
    }

    private static bool IsEditableTextHwnd(IntPtr hwnd)
    {
        string cls = Win32.GetWindowClassName(hwnd);
        if (string.IsNullOrEmpty(cls)) return false;
        if (cls.Equals("Edit", StringComparison.OrdinalIgnoreCase))
        {
            uint style = unchecked((uint)Win32.GetWindowLongPtr(hwnd, Win32.GWL_STYLE).ToInt64());
            return (style & Win32.ES_READONLY) == 0;
        }
        return cls.Contains("RichEdit", StringComparison.OrdinalIgnoreCase);
    }
}
