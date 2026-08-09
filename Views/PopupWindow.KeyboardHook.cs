using System.Runtime.InteropServices;
using System.Windows;

namespace ClipboardManager;

public partial class PopupWindow : Window
{
    private IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode < 0)
            return Win32.CallNextHookEx(_keyboardHook, nCode, wParam, lParam);

        var msg = wParam.ToInt32();
        var kb = Marshal.PtrToStructure<Win32.KBDLLHOOKSTRUCT>(lParam);
        var isKeyDown = msg is Win32.WM_KEYDOWN or Win32.WM_SYSKEYDOWN;
        var isKeyUp = msg is Win32.WM_KEYUP or Win32.WM_SYSKEYUP;

        if (isKeyDown
            && kb.vkCode == Win32.VK_C
            && (Win32.GetAsyncKeyState(Win32.VK_CONTROL) & 0x8000) != 0
            && !_isPopupVisible
            && (kb.flags & Win32.LLKHF_INJECTED) == 0)
        {
            _sourceTracker.NoteCopyShortcut();
        }

        // 排除列表中的应用：直接放行，不做任何拦截
        if (IsForegroundAppExcluded(_appSettings))
            return Win32.CallNextHookEx(_keyboardHook, nCode, wParam, lParam);

        if (TryHandleGlobalKeyboardHookBeforePopup(nCode, wParam, lParam, kb, isKeyDown, isKeyUp, out var result))
            return result;

        if (!_isPopupVisible)
            return Win32.CallNextHookEx(_keyboardHook, nCode, wParam, lParam);

        // 允许后弹出的外部 launcher 取得输入所有权；必须早于菜单/编辑器分支，
        // 否则 ClipboardX 的任一子面板都可能吞掉 launcher 的私有触发键。
        if (ExternalLauncherHotkeyHelper.IsTriggerKey(kb.vkCode))
            return Win32.CallNextHookEx(_keyboardHook, nCode, wParam, lParam);

        UpdatePassthroughModifierLatch(kb.vkCode, isKeyDown, isKeyUp);

        if (_activeFileJumpPicker != null && !_isFileJumpSearchPasteRoutingActive)
            return Win32.CallNextHookEx(_keyboardHook, nCode, wParam, lParam);

        if (TryHandleTextEntryEditPopupKey(nCode, wParam, lParam, kb, isKeyDown, out result))
            return result;

        if (TryHandleMenuAltKeyUp(nCode, wParam, lParam, kb, isKeyUp, out result))
            return result;

        if (!isKeyDown)
            return Win32.CallNextHookEx(_keyboardHook, nCode, wParam, lParam);

        if (TryHandlePhraseEditPopupKey(nCode, wParam, lParam, kb, out result))
            return result;

        if (TryHandleBatchMenuPopupKey(kb, out result))
            return result;

        if (TryHandleContextMenuPopupKey(kb, out result))
            return result;

        return HandleMainPopupKeyDown(nCode, wParam, lParam, kb);
    }

    private void UpdatePassthroughModifierLatch(uint vk, bool isKeyDown, bool isKeyUp)
    {
        var bit = KeyPassthroughHelper.VkToModifierLatchBit(vk);
        if (bit == 0) return;
        if (isKeyDown && (_appSettings?.KeyPassthroughEnabled ?? false))
            _passthroughModifierLatch |= bit;
        else if (isKeyUp)
        {
            _passthroughModifierLatch &= ~bit;
            if (KeyPassthroughHelper.IsModifierFamilyPhysicallyDown(bit))
                _passthroughModifierLatch |= bit;
        }
    }
}
