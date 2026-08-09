using System.Windows;
using SearchEditCommand = ClipboardManager.SearchEditKeyCommandResolver.Command;
using SearchEditCommandKind = ClipboardManager.SearchEditKeyCommandResolver.CommandKind;

namespace ClipboardManager;

public partial class PopupWindow : Window
{
    private bool TryHandleTextEntryEditPopupKey(
        int nCode,
        IntPtr wParam,
        IntPtr lParam,
        Win32.KBDLLHOOKSTRUCT kb,
        bool isKeyDown,
        out IntPtr result)
    {
        if (!_isTextEntryEditPopupOpen)
        {
            result = IntPtr.Zero;
            return false;
        }

        if (isKeyDown)
        {
            if (kb.vkCode == Win32.VK_ESCAPE)
            {
                Dispatcher.BeginInvoke(CancelEntryTextEdit);
                result = (IntPtr)1;
                return true;
            }

            if (kb.vkCode == Win32.VK_RETURN && IsPhysicalCtrlDown())
            {
                Dispatcher.BeginInvoke(CommitEntryTextEdit);
                result = (IntPtr)1;
                return true;
            }
        }

        result = Win32.CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
        return true;
    }

    private bool TryHandlePhraseEditPopupKey(
        int nCode,
        IntPtr wParam,
        IntPtr lParam,
        Win32.KBDLLHOOKSTRUCT kb,
        out IntPtr result)
    {
        if (!_isPhraseEditPopupOpen)
        {
            result = IntPtr.Zero;
            return false;
        }

        if (kb.vkCode == Win32.VK_ESCAPE)
        {
            Dispatcher.BeginInvoke(ResetPhraseEditState);
            result = (IntPtr)1;
            return true;
        }
        if (kb.vkCode == Win32.VK_RETURN)
        {
            Dispatcher.BeginInvoke(CommitPhraseEdit);
            result = (IntPtr)1;
            return true;
        }

        if (kb.vkCode is 0x10 or 0x11 or 0x14
            or 0xA0 or 0xA1 or 0xA2 or 0xA3
            or 0x5B or 0x5C)
        {
            result = Win32.CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
            return true;
        }

        bool phraseCtrlDown = (Win32.GetAsyncKeyState(0x11) & 0x8000) != 0;
        bool phraseAltDown = ((Win32.GetAsyncKeyState(0x12) & 0x8000) != 0)
            || ((Win32.GetAsyncKeyState(0xA4) & 0x8000) != 0)
            || ((Win32.GetAsyncKeyState(0xA5) & 0x8000) != 0);
        bool phraseWinDown = ((Win32.GetAsyncKeyState(0x5B) & 0x8000) != 0)
            || ((Win32.GetAsyncKeyState(0x5C) & 0x8000) != 0);
        var shiftHeld = IsPhysicalShiftDown();

        if (shiftHeld && !phraseCtrlDown && !phraseAltDown && !phraseWinDown
            && kb.vkCode == Win32.VK_INSERT)
        {
            Dispatcher.BeginInvoke(() => _ = PasteSystemClipboardIntoPhraseEditAsync());
            result = (IntPtr)1;
            return true;
        }
        if (shiftHeld && !phraseCtrlDown && !phraseAltDown && !phraseWinDown
            && kb.vkCode == Win32.VK_DELETE)
        {
            Dispatcher.BeginInvoke(() => _ = CutPhraseEditSelectionAsync());
            result = (IntPtr)1;
            return true;
        }

        if (phraseCtrlDown && !phraseAltDown && !phraseWinDown)
        {
            var command = SearchEditKeyCommandResolver.Resolve(kb.vkCode, shiftHeld);
            if (command.IsHandled)
            {
                Dispatcher.BeginInvoke(() => ApplyPhraseEditKeyCommand(command));
                result = (IntPtr)1;
                return true;
            }
        }

        if (phraseAltDown || phraseWinDown || phraseCtrlDown)
        {
            result = Win32.CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
            return true;
        }

        if (kb.vkCode == Win32.VK_BACK)
        {
            Dispatcher.BeginInvoke(() => DeletePhraseEditBackward(ctrl: false));
            result = (IntPtr)1;
            return true;
        }

        if (kb.vkCode == 0x09)
        {
            result = (IntPtr)1;
            return true;
        }

        if (kb.vkCode == Win32.VK_SPACE)
        {
            Dispatcher.BeginInvoke(() => InsertPhraseEditChar(' '));
            result = (IntPtr)1;
            return true;
        }

        switch (kb.vkCode)
        {
            case Win32.VK_LEFT:
                Dispatcher.BeginInvoke(() => MovePhraseEditCaretLeft(ctrl: false, shift: IsPhysicalShiftDown()));
                result = (IntPtr)1;
                return true;
            case Win32.VK_RIGHT:
                Dispatcher.BeginInvoke(() => MovePhraseEditCaretRight(ctrl: false, shift: IsPhysicalShiftDown()));
                result = (IntPtr)1;
                return true;
            case Win32.VK_HOME:
                Dispatcher.BeginInvoke(() => MovePhraseEditCaret(0, IsPhysicalShiftDown()));
                result = (IntPtr)1;
                return true;
            case Win32.VK_END:
                Dispatcher.BeginInvoke(() => MovePhraseEditCaret(_phraseEditor.Text.Length, IsPhysicalShiftDown()));
                result = (IntPtr)1;
                return true;
            case Win32.VK_DELETE:
                Dispatcher.BeginInvoke(() => DeletePhraseEditForward(ctrl: false));
                result = (IntPtr)1;
                return true;
            case Win32.VK_UP:
            case Win32.VK_DOWN:
            case Win32.VK_PRIOR:
            case Win32.VK_NEXT:
                result = (IntPtr)1;
                return true;
        }

        var pch = VkToChar(kb.vkCode, kb.scanCode);
        if (pch.HasValue)
            Dispatcher.BeginInvoke(() => InsertPhraseEditChar(pch.Value));

        result = (IntPtr)1;
        return true;
    }

    private void ApplyPhraseEditKeyCommand(SearchEditCommand command)
    {
        switch (command.Kind)
        {
            case SearchEditCommandKind.MoveBoundary:
                MovePhraseEditCaret(command.Value != 0 ? _phraseEditor.Text.Length : 0, command.Shift);
                break;
            case SearchEditCommandKind.Paste:
                _ = PasteSystemClipboardIntoPhraseEditAsync();
                break;
            case SearchEditCommandKind.SelectAll:
                SelectAllPhraseEditText();
                break;
            case SearchEditCommandKind.Copy:
                _ = CopyPhraseEditSelectionAsync();
                break;
            case SearchEditCommandKind.Cut:
                _ = CutPhraseEditSelectionAsync();
                break;
            case SearchEditCommandKind.Redo:
                RedoPhraseEdit();
                break;
            case SearchEditCommandKind.Undo:
                UndoPhraseEdit();
                break;
            case SearchEditCommandKind.MoveCaret:
                if (command.Value < 0)
                    MovePhraseEditCaretLeft(ctrl: true, shift: command.Shift);
                else
                    MovePhraseEditCaretRight(ctrl: true, shift: command.Shift);
                break;
            case SearchEditCommandKind.Delete:
                if (command.Value < 0)
                    DeletePhraseEditBackward(ctrl: true);
                else
                    DeletePhraseEditForward(ctrl: true);
                break;
        }
    }
}
