using System.Text;
using System.Windows;
using System.Windows.Threading;
using ClipboardManager.Models;
using KeyCommand = ClipboardManager.FileJumpPickerKeyCommandResolver.Command;
using KeyCommandKind = ClipboardManager.FileJumpPickerKeyCommandResolver.CommandKind;
using KeyCommandContext = ClipboardManager.FileJumpPickerKeyCommandResolver.Context;

namespace ClipboardManager;

public partial class FileDialogJumpPickerWindow : Window
{
    private KeyCommand ResolveKeyCommand(uint vk, uint scan)
    {
        bool ctrl = (Win32.GetAsyncKeyState(0x11) & 0x8000) != 0;
        bool alt = (Win32.GetAsyncKeyState(0x12) & 0x8000) != 0;
        bool shift = IsPhysicalShiftDown();
        var removeRecentHotkeyMatch = IsConfiguredHotkeyMatch(
                _settings.FileJumpRemoveRecentHotkeyModifiers,
                _settings.FileJumpRemoveRecentHotkeyKey,
                vk)
            && !(_hasSearchText && _settings.FileJumpRemoveRecentHotkeyModifiers == 0);

        var command = FileJumpPickerKeyCommandResolver.Resolve(new KeyCommandContext(
            vk,
            ctrl,
            alt,
            shift,
            IsPanelModifierMatch(),
            IsStarToggleHotkeyMatch(vk),
            IsConfiguredHotkeyMatch(
                _settings.FileJumpEditPhraseHotkeyModifiers,
                _settings.FileJumpEditPhraseHotkeyKey,
                vk),
            removeRecentHotkeyMatch,
            _hasSearchText));

        return command.IsHandled
            ? command
            : FileJumpPickerKeyCommandResolver.ResolveCharacter(VkToChar(vk, scan));
    }

    private void ApplyKeyCommand(KeyCommand command)
    {
        switch (command.Kind)
        {
            case KeyCommandKind.ToggleFavorite:
                ToggleFavoriteForCurrentSelection();
                break;
            case KeyCommandKind.EditPhrase:
                EditPhraseForCurrentSelection();
                break;
            case KeyCommandKind.RemoveRecent:
                RemoveRecentForCurrentSelection();
                break;
            case KeyCommandKind.PasteClipboardIntoSearch:
                _ = PasteSystemClipboardIntoSearchAsync();
                break;
            case KeyCommandKind.CopySearchSelection:
                _ = CopySearchSelectionAsync();
                break;
            case KeyCommandKind.CutSearchSelection:
                _ = CutSearchSelectionAsync();
                break;
            case KeyCommandKind.JumpQuickIndex:
                JumpByQuickIndex(command.Value);
                break;
            case KeyCommandKind.ToggleFavoritesFilter:
                ToggleFavoritesFilter();
                break;
            case KeyCommandKind.ScrollPage:
                if (command.FlushSearch)
                    FlushPendingSearchRefresh();
                ScrollPage(command.Value);
                break;
            case KeyCommandKind.MoveBoundary:
                HandleSearchOrListBoundary(
                    moveToEnd: command.Value != 0,
                    extendSelection: command.Shift);
                break;
            case KeyCommandKind.SelectAllSearchText:
                SelectAllSearchText();
                break;
            case KeyCommandKind.RedoSearchEdit:
                RedoSearchEdit();
                break;
            case KeyCommandKind.UndoSearchEdit:
                UndoSearchEdit();
                break;
            case KeyCommandKind.MoveSearchCaret:
                if (command.Value < 0)
                    MoveSearchCaretLeft(command.Ctrl, command.Shift);
                else
                    MoveSearchCaretRight(command.Ctrl, command.Shift);
                break;
            case KeyCommandKind.DeleteSearch:
                if (command.Value < 0)
                    DeleteSearchBackward(command.Ctrl);
                else
                    DeleteSearchForward(command.Ctrl);
                break;
            case KeyCommandKind.MoveSelection:
                FlushPendingSearchRefresh();
                MoveSelection(command.Value);
                break;
            case KeyCommandKind.CommitSelection:
                FlushPendingSearchRefresh();
                if (ItemsList.SelectedItem is FileJumpPickerRow row)
                    CommitSelection(row.Path, command.PasteText);
                break;
            case KeyCommandKind.TogglePreview:
                ToggleJumpPreviewBubble();
                break;
            case KeyCommandKind.Escape:
                if (FileJumpShortcutHelpPopup.IsOpen)
                    FileJumpShortcutHelpPopup.IsOpen = false;
                else if (JumpPreviewPopup.IsOpen)
                    CloseJumpPreviewBubble();
                else if (_searchText.Length > 0)
                    ClearSearchText();
                else
                {
                    _restoreOwnerFocusOnClose = true;
                    Close();
                }
                break;
            case KeyCommandKind.InsertCharacter:
                InsertSearchChar(command.Character);
                break;
        }
    }

    private void ScheduleSearchRefresh()
    {
        _searchRefreshTimer?.Stop();
        _searchRefreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(30)
        };
        _searchRefreshTimer.Tick += (_, _) =>
        {
            _searchRefreshTimer?.Stop();
            _searchRefreshTimer = null;
            if (!IsLoaded) return;
            RefreshFilter();
        };
        _searchRefreshTimer.Start();
    }

    private void FlushPendingSearchRefresh()
    {
        if (_searchRefreshTimer == null) return;
        _searchRefreshTimer.Stop();
        _searchRefreshTimer = null;
        RefreshFilter();
    }

    private static char? VkToChar(uint vkCode, uint scanCode)
    {
        var keyState = new byte[256];
        if ((Win32.GetAsyncKeyState(0x10) & 0x8000) != 0) { keyState[0x10] = 0x80; keyState[0xA0] = 0x80; }
        if ((Win32.GetKeyState(0x14) & 0x0001) != 0) keyState[0x14] = 0x01;

        var sb = new StringBuilder(4);
        int result = Win32.ToUnicode(vkCode, scanCode, keyState, sb, sb.Capacity, 0);
        if (result < 0) Win32.ToUnicode(vkCode, scanCode, keyState, sb, sb.Capacity, 0);
        if (result == 1 && !char.IsControl(sb[0])) return sb[0];
        return null;
    }

    private void HandleSearchOrListBoundary(bool moveToEnd, bool extendSelection)
    {
        if (_searchText.Length > 0)
        {
            MoveSearchCaret(moveToEnd ? _searchText.Length : 0, extendSelection);
            return;
        }

        FlushPendingSearchRefresh();
        MoveSelectionToBoundary(moveToEnd);
    }
}

