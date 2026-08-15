using System.Windows;

namespace ClipboardManager;

public partial class PopupWindow : Window
{
    private IntPtr HandleMainPopupKeyDown(
        int nCode,
        IntPtr wParam,
        IntPtr lParam,
        Win32.KBDLLHOOKSTRUCT kb)
    {
        if (IsMenuAltVk(kb.vkCode) && !_isPhraseEditPopupOpen && !_isTextEntryEditPopupOpen && !_isContextPopupOpen && !_isBatchMenuPopupOpen)
        {
            _swallowedMenuAltLatch = true;
            if (!_awaitHotkeyAltChordCleanup)
            {
                _ctxAltAwaitRelease = true;
                _ctxAltComboDuringRelease = false;
            }
            // 吞掉 Alt 按下，不透传到宿主，避免 Word/浏览器等抢菜单焦点、Access Key 导致剪贴板面板失焦。
            // 单按 Alt 松开后仍由上方 KeyUp 分支打开本面板右键/批量菜单（_ctxAltAwaitRelease）。
            return (IntPtr)1;
        }

        if (_ctxAltAwaitRelease && !IsMenuAltVk(kb.vkCode))
            _ctxAltComboDuringRelease = true;

        if (kb.vkCode is 0x10 or 0x11 or 0x14
            or 0xA0 or 0xA1 or 0xA2 or 0xA3
            or 0x5B or 0x5C)
            return Win32.CallNextHookEx(_keyboardHook, nCode, wParam, lParam);

        bool ctrlHeld = IsPhysicalCtrlDown();
        bool altHeld = AltEffectiveForRegisteredChord();
        bool shiftHeld = IsPhysicalShiftDown();

        // Alt+Shift+Enter 原先只是 Alt+Enter「换行连贴」的重复入口；现在将这个精确组合
        // 保留为独立的 URL 后处理模式，并优先于用户可配置的条目动作。
        if (kb.vkCode == Win32.VK_RETURN && altHeld && shiftHeld && !ctrlHeld)
        {
            var openUrlsCommand = PopupMainKeyCommandResolver.Resolve(
                new PopupMainKeyCommandResolver.Context(
                    kb.vkCode,
                    ctrlHeld,
                    altHeld,
                    shiftHeld,
                    IsPanelModifierDown()));
            Dispatcher.BeginInvoke(() => ApplyMainKeyCommand(openUrlsCommand));
            return (IntPtr)1;
        }

        if (HotkeyChordMatches(_panelPageScrollUpModifiers) && kb.vkCode == _panelPageScrollUpKey)
        {
            Dispatcher.BeginInvoke(() => ScrollPage(-1));
            return (IntPtr)1;
        }

        if (HotkeyChordMatches(_panelPageScrollDownModifiers) && kb.vkCode == _panelPageScrollDownKey)
        {
            Dispatcher.BeginInvoke(() => ScrollPage(1));
            return (IntPtr)1;
        }

        if (TryDispatchClipboardItemActionHotkey(kb.vkCode))
            return (IntPtr)1;

        if (HotkeyChordMatches(_starToggleHotkeyModifiers) && kb.vkCode == _starToggleHotkeyKey)
        {
            Dispatcher.BeginInvoke(ToggleStarForCurrentSelection);
            return (IntPtr)1;
        }

        // 必须在 IsPanelModifierDown / ctrlHeld||altHeld 放行之前：否则 Alt+`、Alt+/ 会 CallNextHookEx 进搜索框打出字符。
        if (TryDispatchRegisteredAppHotkeyChordFromHook(kb.vkCode))
            return (IntPtr)1;

        if (KeyPassthroughHelper.ShouldPassthrough(_appSettings, kb.vkCode, true, _passthroughModifierLatch))
            return Win32.CallNextHookEx(_keyboardHook, nCode, wParam, lParam);

        // WH_KEYBOARD_LL 在 GlobalHookDispatcher 后台线程执行，不能读取 WPF Popup.IsOpen。
        // _previewImageFiles 仅在预览打开时赋值、关闭时清空，可作为线程安全的导航状态快照。
        if (!ctrlHeld && !altHeld && !shiftHeld
            && _previewImageFiles is { Length: > 1 }
            && kb.vkCode is Win32.VK_LEFT or Win32.VK_RIGHT)
        {
            var delta = kb.vkCode == Win32.VK_LEFT ? -1 : 1;
            Dispatcher.BeginInvoke(() => NavigatePreviewImage(delta));
            return (IntPtr)1;
        }
        var command = PopupMainKeyCommandResolver.Resolve(new PopupMainKeyCommandResolver.Context(
            kb.vkCode,
            ctrlHeld,
            altHeld,
            shiftHeld,
            IsPanelModifierDown()));

        if (command.NeedsCharacterTranslation)
        {
            var character = VkToChar(kb.vkCode, kb.scanCode);
            if (AltEffectiveForRegisteredChord())
                return (IntPtr)1;
            command = PopupMainKeyCommandResolver.ResolveCharacter(character);
        }

        if (command.IsPassThrough)
            return Win32.CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
        if (command.IsSwallowedOnly)
            return (IntPtr)1;

        var commandToApply = command;
        Dispatcher.BeginInvoke(() => ApplyMainKeyCommand(commandToApply));
        return (IntPtr)1;
    }

    private void HandleSearchOrListBoundary(bool moveToEnd, bool extendSelection)
    {
        if (_searchText.Length > 0)
        {
            MoveSearchCaret(moveToEnd ? _searchText.Length : 0, extendSelection);
            return;
        }

        if (_selectionCursor.HasPointCursor)
            MoveKeyboardPointSelectionCursorTo(moveToEnd ? _displayItems.Count - 1 : 0);
        else if (moveToEnd)
            MoveSelectionToLast();
        else
            MoveSelectionToFirst();
    }

    private static bool IsPhysicalShiftDown() =>
        (Win32.GetAsyncKeyState(0x10) & 0x8000) != 0
        || (Win32.GetAsyncKeyState(0xA0) & 0x8000) != 0
        || (Win32.GetAsyncKeyState(0xA1) & 0x8000) != 0;

}
