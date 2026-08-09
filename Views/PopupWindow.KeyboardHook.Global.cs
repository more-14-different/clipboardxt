using System.Windows;

namespace ClipboardManager;

public partial class PopupWindow : Window
{
    private bool TryHandleGlobalKeyboardHookBeforePopup(
        int nCode,
        IntPtr wParam,
        IntPtr lParam,
        Win32.KBDLLHOOKSTRUCT kb,
        bool isKeyDown,
        bool isKeyUp,
        out IntPtr result)
    {
        // 拦截 Win+V（系统剪贴板历史快捷键），替换为 ClipboardX
        // Win 键 (0x5B/0x5C) + V 键 (0x56)
        // 仅当设置中启用了 ReplaceSystemWinV 时才拦截
        if (isKeyDown && kb.vkCode == Win32.VK_V && (_appSettings?.ReplaceSystemWinV ?? false))
        {
            bool winDown = (Win32.GetAsyncKeyState(0x5B) & 0x8000) != 0
                || (Win32.GetAsyncKeyState(0x5C) & 0x8000) != 0;
            bool ctrlDown = (Win32.GetAsyncKeyState(0x11) & 0x8000) != 0
                || (Win32.GetAsyncKeyState(0xA2) & 0x8000) != 0
                || (Win32.GetAsyncKeyState(0xA3) & 0x8000) != 0;

            // Win+V 且没有按住 Ctrl（避免拦截 Ctrl+Win+V 等其他组合）
            if (winDown && !ctrlDown)
            {
#if CLIPX_CLIPBOARD
                // 触发剪贴板弹窗
                Dispatcher.BeginInvoke(TogglePopup);
#endif
                _winVIntercepted = true;
                // 拦截按键，不传递给系统
                result = (IntPtr)1;
                return true;
            }
        }

        // Win+V 拦截后：吞掉 Win KeyUp 防止开始菜单弹出，然后注入 Escape（关闭可能闪出的开始菜单）
        // + 合成 Win KeyUp（重置系统 Win 键状态，避免 Win 键"卡住"）
        if (isKeyUp && _winVIntercepted && (kb.vkCode == 0x5B || kb.vkCode == 0x5C))
        {
            _winVIntercepted = false;
            InjectWinKeyUpReset(kb);
            result = (IntPtr)1;
            return true;
        }

#if CLIPX_CLIPBOARD
        // 非本窗口内 Ctrl+V 松 V、或 Shift+Insert 松 Insert：FIFO/LIFO 出队并写下一项到剪贴板；不拦截按键
        if (isKeyUp)
        {
            var injEarly = (kb.flags & (Win32.LLKHF_INJECTED | Win32.LLKHF_LOWER_IL_INJECTED)) != 0;
            if (!injEarly && !_isSettingClipboard && !_pasteInProgress
                && !_isPopupVisible && _activeFileJumpPicker == null)
            {
                bool ctrlNow = (Win32.GetAsyncKeyState(0x11) & 0x8000) != 0
                    || (Win32.GetAsyncKeyState(0xA2) & 0x8000) != 0
                    || (Win32.GetAsyncKeyState(0xA3) & 0x8000) != 0;
                bool shiftNow = (Win32.GetAsyncKeyState(0x10) & 0x8000) != 0
                    || (Win32.GetAsyncKeyState(0xA0) & 0x8000) != 0
                    || (Win32.GetAsyncKeyState(0xA1) & 0x8000) != 0;
                bool pasteChord =
                    (kb.vkCode == Win32.VK_V && ctrlNow)
                    || (kb.vkCode == Win32.VK_INSERT && shiftNow);
                if (pasteChord)
                {
                    var fg = Win32.GetForegroundWindow();
                    if (fg != IntPtr.Zero && fg != _hwnd)
                    {
                        long tick = Environment.TickCount64;
                        if (tick - _lastGlobalPasteQueueAdvanceTick > 120)
                        {
                            if ((GetBatchMode() == BatchPasteQueueMode.Fifo || GetBatchMode() == BatchPasteQueueMode.Lifo)
                                && _batchQueue.Count == 0
                                && (_appSettings?.BatchQueueAutoSwitchToNormalAfterQueueDone ?? true)
                                && _batchQueueAwaitingNextPasteToSwitchOff)
                            {
                                _lastGlobalPasteQueueAdvanceTick = tick;
                                Dispatcher.BeginInvoke(() => SetBatchPasteMode(BatchPasteQueueMode.Off));
                            }
                            else if (GetBatchMode() != BatchPasteQueueMode.Off && _batchQueue.Count > 0)
                            {
                                _lastGlobalPasteQueueAdvanceTick = tick;
                                Dispatcher.BeginInvoke(new Action(TryAdvancePasteQueueAfterGlobalPaste));
                            }
                        }
                    }
                }
            }
        }
#endif

        // 面板未关闭时钩子仍会吃掉未识别的键；多段粘贴中间几次不走 HidePopup，SendInput 的 Shift+Insert 必须放行。
        if ((kb.flags & (Win32.LLKHF_INJECTED | Win32.LLKHF_LOWER_IL_INJECTED)) != 0)
        {
            result = Win32.CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
            return true;
        }

        TryExpireHotkeyAltChordCleanupDeadline();

        if (!_isPopupVisible && _awaitHotkeyAltChordCleanup)
        {
            if (_activeFileJumpPicker != null)
            {
                result = Win32.CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
                return true;
            }

#if CLIPX_FILEJUMP
            // 剪贴板浮层仍显示但焦点已在系统资源管理器时，必须放行低级键盘链，
            // 否则后装的钩子无法收到按键（资源管理器内 Everything 筛选等）。
            if (IsSystemExplorerForeground())
            {
                result = Win32.CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
                return true;
            }
#endif

            if (isKeyUp && IsMenuAltVk(kb.vkCode) && !_ctxAltAwaitRelease)
            {
                _awaitHotkeyAltChordCleanup = false;
                _hotkeyAltChordCleanupDeadlineTick = 0;
                _ctxAltComboDuringRelease = false;
                InjectSyntheticHotkeyAltChordCleanup(kb);
#if CLIPX_CLIPBOARD
                SyncBatchPasteKeyboardHook();
#else
                if (!_isPopupVisible)
                    UninstallKeyboardHook();
#endif
                result = (IntPtr)1;
                return true;
            }

            result = Win32.CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
            return true;
        }

        result = IntPtr.Zero;
        return false;
    }
}
