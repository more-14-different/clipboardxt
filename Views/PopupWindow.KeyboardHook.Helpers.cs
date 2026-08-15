using System.Runtime.InteropServices;
using System.Windows;

namespace ClipboardManager;

public partial class PopupWindow : Window
{
    private static bool IsMenuAltVk(uint vk) =>
        vk == 0x12 || vk == 0xA4 || vk == 0xA5;

    /// <summary>批量子菜单/右键菜单打开时：钩子内按设置匹配与 RegisterHotKey 相同的组合（Alt 已由本钩吞掉，宿主收不到）。</summary>
    private bool TryDispatchRegisteredAppHotkeyChordFromHook(uint vkCode)
    {
        if (_appSettings == null) return false;
        if (IsForegroundAppExcluded(_appSettings)) return false;
#if CLIPX_CLIPBOARD
        if (HotkeyChordMatches(_hotkeyModifiers) && vkCode == _hotkeyKey)
        {
            Dispatcher.BeginInvoke(TogglePopup);
            return true;
        }
#endif
        if (HotkeyChordMatches(_appSettings.BatchModeCycleHotkeyModifiers)
            && vkCode == _appSettings.BatchModeCycleHotkeyKey)
        {
            Dispatcher.BeginInvoke(CycleBatchPasteMode);
            return true;
        }
        if (HotkeyChordMatches(_fileJumpHotkeyModifiers) && vkCode == _fileJumpHotkeyKey)
        {
            Dispatcher.BeginInvoke(TryJumpFileDialogToLastFolder);
            return true;
        }
        if (HotkeyChordMatches(_panelPageScrollUpModifiers) && vkCode == _panelPageScrollUpKey)
        {
            Dispatcher.BeginInvoke(() => ScrollPage(-1));
            return true;
        }
        if (HotkeyChordMatches(_panelPageScrollDownModifiers) && vkCode == _panelPageScrollDownKey)
        {
            Dispatcher.BeginInvoke(() => ScrollPage(1));
            return true;
        }
        return false;
    }

    /// <summary>
    /// Win+V 拦截后吞掉 Win KeyUp，注入 Escape（关闭可能闪出的开始菜单）+ 合成 Win KeyUp（重置系统 Win 键状态）。
    /// </summary>
    private static void InjectWinKeyUpReset(Win32.KBDLLHOOKSTRUCT kb)
    {
        uint ext = (kb.flags & 0x01) != 0 ? Win32.KEYEVENTF_EXTENDEDKEY : 0u;
        var inputs = new Win32.INPUT[3];

        // Escape：关闭可能闪出的开始菜单
        inputs[0].type = Win32.INPUT_KEYBOARD;
        inputs[0].u.ki.wVk = (ushort)Win32.VK_ESCAPE;
        inputs[0].u.ki.wScan = 0;
        inputs[0].u.ki.dwFlags = 0;
        inputs[0].u.ki.time = 0;
        inputs[0].u.ki.dwExtraInfo = IntPtr.Zero;

        inputs[1].type = Win32.INPUT_KEYBOARD;
        inputs[1].u.ki.wVk = (ushort)Win32.VK_ESCAPE;
        inputs[1].u.ki.wScan = 0;
        inputs[1].u.ki.dwFlags = Win32.KEYEVENTF_KEYUP;
        inputs[1].u.ki.time = 0;
        inputs[1].u.ki.dwExtraInfo = IntPtr.Zero;

        // 合成 Win KeyUp：重置系统 Win 键状态
        inputs[2].type = Win32.INPUT_KEYBOARD;
        inputs[2].u.ki.wVk = (ushort)kb.vkCode;
        inputs[2].u.ki.wScan = 0;
        inputs[2].u.ki.dwFlags = Win32.KEYEVENTF_KEYUP | ext;
        inputs[2].u.ki.time = 0;
        inputs[2].u.ki.dwExtraInfo = IntPtr.Zero;

        Win32.SendInput(3, inputs, Marshal.SizeOf<Win32.INPUT>());
    }

    private static void InjectSyntheticHotkeyAltChordCleanup(Win32.KBDLLHOOKSTRUCT kb)
    {
        uint ext = (kb.flags & 0x01) != 0 ? Win32.KEYEVENTF_EXTENDEDKEY : 0u;

        // 呼出热键中的 Ctrl 可能仍被用户物理按住。此时合成 Ctrl Up 会让
        // GetAsyncKeyState(Ctrl) 在下一次物理 Ctrl 事件前错误地报告为松开，
        // 导致面板内紧接着的 Ctrl+H/J/K/L 被当成普通输入。
        // 物理 Ctrl 本身已足以避免 Alt KeyUp 激活宿主菜单，因此只需补 Alt KeyUp。
        if (IsPhysicalCtrlDown())
        {
            var altUp = new Win32.INPUT
            {
                type = Win32.INPUT_KEYBOARD
            };
            altUp.u.ki.wVk = (ushort)kb.vkCode;
            altUp.u.ki.wScan = 0;
            altUp.u.ki.dwFlags = Win32.KEYEVENTF_KEYUP | ext;
            altUp.u.ki.time = 0;
            altUp.u.ki.dwExtraInfo = IntPtr.Zero;

            Win32.SendInput(1, [altUp], Marshal.SizeOf<Win32.INPUT>());
            return;
        }

        var inputs = new Win32.INPUT[3];

        inputs[0].type = Win32.INPUT_KEYBOARD;
        inputs[0].u.ki.wVk = Win32.VK_CONTROL;
        inputs[0].u.ki.wScan = 0;
        inputs[0].u.ki.dwFlags = 0;
        inputs[0].u.ki.time = 0;
        inputs[0].u.ki.dwExtraInfo = IntPtr.Zero;

        inputs[1].type = Win32.INPUT_KEYBOARD;
        inputs[1].u.ki.wVk = Win32.VK_CONTROL;
        inputs[1].u.ki.wScan = 0;
        inputs[1].u.ki.dwFlags = Win32.KEYEVENTF_KEYUP;
        inputs[1].u.ki.time = 0;
        inputs[1].u.ki.dwExtraInfo = IntPtr.Zero;

        inputs[2].type = Win32.INPUT_KEYBOARD;
        inputs[2].u.ki.wVk = (ushort)kb.vkCode;
        inputs[2].u.ki.wScan = 0;
        inputs[2].u.ki.dwFlags = Win32.KEYEVENTF_KEYUP | ext;
        inputs[2].u.ki.time = 0;
        inputs[2].u.ki.dwExtraInfo = IntPtr.Zero;

        Win32.SendInput(3, inputs, Marshal.SizeOf<Win32.INPUT>());
    }

#if CLIPX_FILEJUMP
    private static bool IsSystemExplorerForeground()
    {
        var fg = Win32.GetForegroundWindow();
        return FileManagerPathCollector.TryFindExplorerCabinetFrame(fg) != IntPtr.Zero;
    }
#endif

    private static bool IsPhysicalCtrlDown() =>
        (Win32.GetAsyncKeyState(0x11) & 0x8000) != 0
        || (Win32.GetAsyncKeyState(0xA2) & 0x8000) != 0
        || (Win32.GetAsyncKeyState(0xA3) & 0x8000) != 0;

    private bool AltPhysicallyDown() =>
        ((Win32.GetAsyncKeyState(0x12) & 0x8000) != 0)
        || ((Win32.GetAsyncKeyState(0xA4) & 0x8000) != 0)
        || ((Win32.GetAsyncKeyState(0xA5) & 0x8000) != 0);

    /// <summary>物理 Alt 或主面板吞 Alt Down 后的锁存（吞键后 GetAsyncKeyState(Alt) 常为假，导致 Alt+/ 误进搜索）。</summary>
    private bool AltEffectiveForRegisteredChord() => AltPhysicallyDown() || _swallowedMenuAltLatch;

    private bool IsRightAltEffective() =>
        _swallowedRightAltLatch || (Win32.GetAsyncKeyState(Win32.VK_RMENU) & 0x8000) != 0;

    /// <summary>
    /// 与 RegisterHotKey 的 fsModifiers 一致；含 <see cref="_swallowedMenuAltLatch"/> 与 AltGr（LCtrl+RAlt）兜底。
    /// </summary>
    private bool HotkeyChordMatches(uint requiredMods)
    {
        bool ctrl = IsPhysicalCtrlDown();
        bool shift = ((Win32.GetAsyncKeyState(0x10) & 0x8000) != 0)
            || ((Win32.GetAsyncKeyState(0xA0) & 0x8000) != 0)
            || ((Win32.GetAsyncKeyState(0xA1) & 0x8000) != 0);
        bool alt = AltEffectiveForRegisteredChord();
        bool win = ((Win32.GetAsyncKeyState(0x5B) & 0x8000) != 0)
            || ((Win32.GetAsyncKeyState(0x5C) & 0x8000) != 0);
        bool reqCtrl = (requiredMods & Win32.MOD_CONTROL) != 0;
        bool reqShift = (requiredMods & Win32.MOD_SHIFT) != 0;
        bool reqAlt = (requiredMods & Win32.MOD_ALT) != 0;
        bool reqWin = (requiredMods & Win32.MOD_WIN) != 0;
        if (ctrl == reqCtrl && shift == reqShift && alt == reqAlt && win == reqWin)
            return true;
        if ((requiredMods & Win32.MOD_ALT) == 0 || (requiredMods & Win32.MOD_CONTROL) != 0)
            return false;
        bool physAlt = AltPhysicallyDown();
        if (shift != reqShift || win != reqWin)
            return false;
        return physAlt && IsPhysicalCtrlDown();
    }

    private bool IsPanelModifierDown()
    {
        bool ctrl = (Win32.GetAsyncKeyState(0x11) & 0x8000) != 0;
        bool alt = (Win32.GetAsyncKeyState(0x12) & 0x8000) != 0;
        bool win = ((Win32.GetAsyncKeyState(0x5B) | Win32.GetAsyncKeyState(0x5C)) & 0x8000) != 0;
        bool caps = (Win32.GetAsyncKeyState(0x14) & 0x8000) != 0;

        return _panelModifierKey switch
        {
            "Alt" => alt && !ctrl,
            "Win" => win && !ctrl && !alt,
            "CapsLock" => caps && !ctrl && !alt,
            _ => ctrl && !alt,
        };
    }

    private string PanelModifierDisplayName => _panelModifierKey switch
    {
        "Alt" => "Alt",
        "Win" => "Win",
        "CapsLock" => "CapsLk",
        _ => "Ctrl",
    };

}
