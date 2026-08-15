using System.Windows;

namespace ClipboardManager;

public partial class PopupWindow : Window
{
    private bool TryHandleMenuAltKeyUp(
        int nCode,
        IntPtr wParam,
        IntPtr lParam,
        Win32.KBDLLHOOKSTRUCT kb,
        bool isKeyUp,
        out IntPtr result)
    {
        if (!isKeyUp || !IsMenuAltVk(kb.vkCode))
        {
            result = IntPtr.Zero;
            return false;
        }

        _swallowedMenuAltLatch = false;
        if (_isPhraseEditPopupOpen)
        {
            result = Win32.CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
            return true;
        }

        if (_awaitHotkeyAltChordCleanup && !_ctxAltAwaitRelease)
        {
            _awaitHotkeyAltChordCleanup = false;
            _hotkeyAltChordCleanupDeadlineTick = 0;
            _ctxAltComboDuringRelease = false;
            InjectSyntheticHotkeyAltChordCleanup(kb);
            result = (IntPtr)1;
            return true;
        }

        if (_isBatchMenuPopupOpen)
        {
            if (_ctxAltCloseMenuArmed && !_ctxAltComboDuringRelease)
                Dispatcher.BeginInvoke(() => { BatchMenuPopup.IsOpen = false; CloseBatchMenuNavUi(); });
            _ctxAltCloseMenuArmed = false;
            _ctxAltAwaitRelease = false;
            _ctxAltComboDuringRelease = false;
            result = (IntPtr)1;
            return true;
        }

        if (_isContextPopupOpen)
        {
            if (_ctxAltCloseMenuArmed && !_ctxAltComboDuringRelease)
                Dispatcher.BeginInvoke(CloseContextMenuPopup);
            _ctxAltCloseMenuArmed = false;
            _ctxAltAwaitRelease = false;
            _ctxAltComboDuringRelease = false;
            result = (IntPtr)1;
            return true;
        }

        if (_ctxAltAwaitRelease && !_ctxAltComboDuringRelease)
            Dispatcher.BeginInvoke(TryOpenBatchOrContextMenuFromKeyboard);
        _ctxAltAwaitRelease = false;
        _ctxAltComboDuringRelease = false;
        // 吞掉 Alt 松开，避免宿主在未收到 Down 时仍收到 Up，或双次触发菜单栏
        result = (IntPtr)1;
        return true;
    }

    private bool TryHandleBatchMenuPopupKey(Win32.KBDLLHOOKSTRUCT kb, out IntPtr result)
    {
        if (!_isBatchMenuPopupOpen)
        {
            result = IntPtr.Zero;
            return false;
        }

        if (IsMenuAltVk(kb.vkCode))
        {
            _ctxAltCloseMenuArmed = true;
            _ctxAltComboDuringRelease = false;
            result = (IntPtr)1;
            return true;
        }

        if (TryDispatchRegisteredAppHotkeyChordFromHook(kb.vkCode))
        {
            result = (IntPtr)1;
            return true;
        }

        bool altPhyB = ((Win32.GetAsyncKeyState(0x12) & 0x8000) != 0)
            || ((Win32.GetAsyncKeyState(0xA4) & 0x8000) != 0)
            || ((Win32.GetAsyncKeyState(0xA5) & 0x8000) != 0);
        if (_ctxAltCloseMenuArmed && altPhyB)
            _ctxAltComboDuringRelease = true;

        switch (kb.vkCode)
        {
            case Win32.VK_UP:
                Dispatcher.BeginInvoke(() => MoveBatchMenuHighlight(-1));
                result = (IntPtr)1;
                return true;
            case Win32.VK_DOWN:
                Dispatcher.BeginInvoke(() => MoveBatchMenuHighlight(1));
                result = (IntPtr)1;
                return true;
            case Win32.VK_RETURN:
                Dispatcher.BeginInvoke(ActivateBatchMenuHighlight);
                result = (IntPtr)1;
                return true;
            case Win32.VK_ESCAPE:
                Dispatcher.BeginInvoke(() => { BatchMenuPopup.IsOpen = false; CloseBatchMenuNavUi(); });
                result = (IntPtr)1;
                return true;
            default:
                result = (IntPtr)1;
                return true;
        }
    }

    private bool TryHandleContextMenuPopupKey(Win32.KBDLLHOOKSTRUCT kb, out IntPtr result)
    {
        if (!_isContextPopupOpen)
        {
            result = IntPtr.Zero;
            return false;
        }

        if (IsMenuAltVk(kb.vkCode))
        {
            _ctxAltCloseMenuArmed = true;
            _ctxAltComboDuringRelease = false;
            result = (IntPtr)1;
            return true;
        }

        if (TryDispatchRegisteredAppHotkeyChordFromHook(kb.vkCode))
        {
            result = (IntPtr)1;
            return true;
        }

        // 无修饰 Enter 始终确认当前高亮菜单项；否则默认“粘贴 = Enter”会让菜单无法键盘导航。
        if (!(kb.vkCode == Win32.VK_RETURN && HotkeyChordMatches(0))
            && TryDispatchClipboardItemActionHotkey(kb.vkCode))
        {
            result = (IntPtr)1;
            return true;
        }

        bool altPhy = ((Win32.GetAsyncKeyState(0x12) & 0x8000) != 0)
            || ((Win32.GetAsyncKeyState(0xA4) & 0x8000) != 0)
            || ((Win32.GetAsyncKeyState(0xA5) & 0x8000) != 0);
        if (_ctxAltCloseMenuArmed && altPhy)
            _ctxAltComboDuringRelease = true;

        switch (kb.vkCode)
        {
            case Win32.VK_UP:
                Dispatcher.BeginInvoke(() => MoveContextMenuHighlight(-1));
                result = (IntPtr)1;
                return true;
            case Win32.VK_DOWN:
                Dispatcher.BeginInvoke(() => MoveContextMenuHighlight(1));
                result = (IntPtr)1;
                return true;
            case Win32.VK_RETURN:
                Dispatcher.BeginInvoke(ActivateContextMenuHighlight);
                result = (IntPtr)1;
                return true;
            case Win32.VK_ESCAPE:
                Dispatcher.BeginInvoke(CloseContextMenuPopup);
                result = (IntPtr)1;
                return true;
            default:
                result = (IntPtr)1;
                return true;
        }
    }
}
