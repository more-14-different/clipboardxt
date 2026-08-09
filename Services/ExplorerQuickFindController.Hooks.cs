using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace ClipboardManager;

public sealed partial class ExplorerQuickFindController : IDisposable
{
    private static IntPtr StaticHookProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        var o = s_owner;
        if (o == null) return Win32.CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
        try
        {
            return o.HookProc(nCode, wParam, lParam);
        }
        catch (Exception ex)
        {
            ClipboardDiagnosticsLog.Write($"native Explorer quick-find keyboard hook exception: {ex}");
            return Win32.CallNextHookEx(o._hook, nCode, wParam, lParam);
        }
    }

    // 用于在分支方法中传递原始钩参数给 CallNextHookEx
    private int _hookNCode;
    private IntPtr _hookWParam;
    private IntPtr _hookLParam;

    /// <summary>
    /// 低级键盘钩回调。设计为 &lt;1ms：仅读 Win32 缓存状态，不做 COM/UIA/UI。
    /// 吞键后通过 BeginInvoke 异步处理。
    /// </summary>
    private IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode < 0 || !_settings.ExplorerEverythingQuickFindEnabled)
            return Win32.CallNextHookEx(_hook, nCode, wParam, lParam);

        if (wParam != (IntPtr)Win32.WM_KEYDOWN)
            return Win32.CallNextHookEx(_hook, nCode, wParam, lParam);

        var kb = Marshal.PtrToStructure<Win32.KBDLLHOOKSTRUCT>(lParam);

        if ((kb.flags & 0x10) != 0) // LLKHF_INJECTED (0x10, not 0x01 which is LLKHF_EXTENDED)
            return Win32.CallNextHookEx(_hook, nCode, wParam, lParam);

        if (ExternalLauncherHotkeyHelper.IsTriggerKey(kb.vkCode))
            return Win32.CallNextHookEx(_hook, nCode, wParam, lParam);

        var fg = Win32.GetForegroundWindow();
        Win32.GetWindowThreadProcessId(fg, out var fgPid);
        if ((int)fgPid == Environment.ProcessId)
        {
            // 弹窗自身获得前台时，仍需拦截 Escape 关闭会话
            if (_sessionActive && kb.vkCode == Win32.VK_ESCAPE)
            {
                _dispatcher.BeginInvoke(EndSession);
                return (IntPtr)1;
            }
            return Win32.CallNextHookEx(_hook, nCode, wParam, lParam);
        }

        _hookNCode = nCode;
        _hookWParam = wParam;
        _hookLParam = lParam;

        if (_sessionActive)
            return HandleSessionKeyInHook(fg, kb);

        return TryStartSessionInHook(fg, kb);
    }

    /// <summary>已在会话中：快速决策是否吞键。会话期间全面接管键盘，防止 Explorer 处理任何按键。</summary>
    private IntPtr HandleSessionKeyInHook(IntPtr fg, Win32.KBDLLHOOKSTRUCT kb)
    {
        if (!IsStillTargetExplorer(fg))
        {
            _dispatcher.BeginInvoke(EndSession);
            return PassThrough();
        }

        if (IsModifierKey(kb.vkCode))
            return (IntPtr)1;

        var ctrl = (Win32.GetAsyncKeyState(0x11) & 0x8000) != 0;
        var alt = (Win32.GetAsyncKeyState(0x12) & 0x8000) != 0;
        var win = (Win32.GetAsyncKeyState(0x5B) & 0x8000) != 0
               || (Win32.GetAsyncKeyState(0x5C) & 0x8000) != 0;

        if (ctrl && !alt && !win && kb.vkCode >= 0x31 && kb.vkCode <= 0x39)
        {
            int idx = (int)(kb.vkCode - 0x31);
            _dispatcher.BeginInvoke(() => QuickSelectAndActivate(idx));
            return (IntPtr)1;
        }

        if (ctrl || alt || win)
        {
            _dispatcher.BeginInvoke(EndSession);
            return PassThrough();
        }

        switch (kb.vkCode)
        {
            case Win32.VK_ESCAPE:
            case Win32.VK_RETURN:
            case Win32.VK_UP:
            case Win32.VK_DOWN:
            case Win32.VK_LEFT:
            case Win32.VK_RIGHT:
            case Win32.VK_BACK:
            case Win32.VK_DELETE:
            case 0x21: // Page Up
            case 0x22: // Page Down
            case 0x24: // Home
            case 0x23: // End
                _dispatcher.BeginInvoke(() => ProcessSessionKey(kb.vkCode));
                return (IntPtr)1;
        }

        CaptureKeyState(kb, out var keyState);
        if (TryGetChar(kb.vkCode, kb.scanCode, keyState, out var ch) && ch >= ' ')
        {
            _dispatcher.BeginInvoke(() => AppendChar(ch));
            return (IntPtr)1;
        }

        return (IntPtr)1;
    }

    private IntPtr PassThrough()
        => Win32.CallNextHookEx(_hook, _hookNCode, _hookWParam, _hookLParam);

    /// <summary>尝试开始新会话：快速判断是否在资源管理器文件列表上下文。</summary>
    private IntPtr TryStartSessionInHook(IntPtr fg, Win32.KBDLLHOOKSTRUCT kb)
    {
        // 修饰键、导航键、功能键等不触发会话
        if (kb.vkCode is Win32.VK_ESCAPE or Win32.VK_RETURN or Win32.VK_BACK
            or Win32.VK_UP or Win32.VK_DOWN or Win32.VK_LEFT or Win32.VK_RIGHT
            or Win32.VK_DELETE
            or >= 0x70 and <= 0x87   // F1-F24
            or 0x09                   // Tab
            or 0x91                   // Scroll Lock
            or 0x90                   // Num Lock
            or 0x2C)                  // Print Screen
            return PassThrough();

        if (IsModifierKey(kb.vkCode))
            return PassThrough();

        var ctrl = (Win32.GetAsyncKeyState(0x11) & 0x8000) != 0;
        var alt = (Win32.GetAsyncKeyState(0x12) & 0x8000) != 0;
        var win = (Win32.GetAsyncKeyState(0x5B) & 0x8000) != 0
               || (Win32.GetAsyncKeyState(0x5C) & 0x8000) != 0;
        if (ctrl || alt || win)
            return PassThrough();

        var cls = Win32.GetWindowClassName(fg);
        var isDesktop = cls.Equals("Progman", StringComparison.OrdinalIgnoreCase)
                     || cls.Equals("WorkerW", StringComparison.OrdinalIgnoreCase);

        IntPtr frame;
        if (isDesktop)
        {
            frame = fg;
        }
        else
        {
            frame = FileManagerPathCollector.TryFindExplorerCabinetFrame(fg);
            if (frame == IntPtr.Zero)
                return PassThrough();

            if (!QuickCheckFocusNotEditBox(frame))
                return PassThrough();
        }

        CaptureKeyState(kb, out var keyState);
        if (!TryGetChar(kb.vkCode, kb.scanCode, keyState, out var ch) || ch < ' ')
            return PassThrough();

        // 通过快速检测：吞键，异步启动会话
        _sessionActive = true;
        _sessionExplorerFrame = frame;
        _dispatcher.BeginInvoke(() => BeginSessionAsync(frame, ch, isDesktop));
        return (IntPtr)1;
    }

    private static bool IsModifierKey(uint vk) => vk is
        0x10 or 0x11 or 0x12 or 0x14           // Shift, Ctrl, Alt, CapsLock
        or 0xA0 or 0xA1 or 0xA2 or 0xA3        // L/R Shift, L/R Ctrl
        or 0xA4 or 0xA5                          // L/R Alt
        or 0x5B or 0x5C;                         // L/R Win
}

