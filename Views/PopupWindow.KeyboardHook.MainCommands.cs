using System.Windows;
using MainCommand = ClipboardManager.PopupMainKeyCommandResolver.Command;
using MainCommandKind = ClipboardManager.PopupMainKeyCommandResolver.CommandKind;
using SearchEditCommand = ClipboardManager.SearchEditKeyCommandResolver.Command;
using SearchEditCommandKind = ClipboardManager.SearchEditKeyCommandResolver.CommandKind;

namespace ClipboardManager;

public partial class PopupWindow : Window
{
    private void ApplyMainKeyCommand(MainCommand command)
    {
        switch (command.Kind)
        {
            case MainCommandKind.InsertCharacter:
                InsertSearchChar(command.Character);
                break;
            case MainCommandKind.PasteClipboardIntoSearch:
                _ = PasteSystemClipboardIntoSearchAsync();
                break;
            case MainCommandKind.CutSearchSelection:
                _ = CutSearchSelectionAsync();
                break;
            case MainCommandKind.SearchEdit:
                ApplySearchEditKeyCommand(command.SearchEdit);
                break;
            case MainCommandKind.PasteByIndex:
                PasteByIndex(command.Value);
                break;
            case MainCommandKind.ToggleQuickPhraseFilter:
                ToggleQuickPhraseFilter();
                break;
            case MainCommandKind.TogglePointSelection:
                ToggleKeyboardPointSelection();
                break;
            case MainCommandKind.CommitSelection:
                HandleMainEnterKey(
                    command.NewlineAfterEachText,
                    command.SoftLineBreakAfterEachText);
                break;
            case MainCommandKind.MoveSelection:
                ApplyMainSelectionMovement(command.Value, command.Shift);
                break;
            case MainCommandKind.MoveSelectionByHjkl:
                ApplyHjklSelectionMovement(command.Value, command.Shift);
                break;
            case MainCommandKind.MoveBoundary:
                HandleSearchOrListBoundary(command.Value != 0, command.Shift);
                break;
            case MainCommandKind.ScrollPage:
                if (_selectionCursor.HasPointCursor)
                    ScrollKeyboardPointSelectionCursorPage(command.Value);
                else
                    ScrollPage(command.Value);
                break;
            case MainCommandKind.MoveCaret:
                if (command.Value < 0)
                    MoveSearchCaretLeft(ctrl: false, shift: command.Shift);
                else
                    MoveSearchCaretRight(ctrl: false, shift: command.Shift);
                break;
            case MainCommandKind.TogglePreview:
                ToggleEntryPreviewBubble();
                break;
            case MainCommandKind.Escape:
                HandleMainEscapeKey();
                break;
            case MainCommandKind.DeleteBackward:
                DeleteSearchBackward(ctrl: false);
                break;
            case MainCommandKind.CycleTypeFilter:
                CycleTypeFilter();
                break;
            case MainCommandKind.DeleteForwardOrItem:
                if (_searchText.Length > 0)
                    DeleteSearchForward(ctrl: false);
                else
                    DeleteSelectedItemWithConfirm();
                break;
        }
    }

    private void ApplySearchEditKeyCommand(SearchEditCommand command)
    {
        switch (command.Kind)
        {
            case SearchEditCommandKind.MoveBoundary:
                HandleSearchOrListBoundary(command.Value != 0, command.Shift);
                break;
            case SearchEditCommandKind.Paste:
                _ = PasteSystemClipboardIntoSearchAsync();
                break;
            case SearchEditCommandKind.SelectAll:
                SelectAllSearchText();
                break;
            case SearchEditCommandKind.Copy:
                _ = CopySearchSelectionAsync();
                break;
            case SearchEditCommandKind.Cut:
                _ = CutSearchSelectionAsync();
                break;
            case SearchEditCommandKind.Redo:
                RedoSearchEdit();
                break;
            case SearchEditCommandKind.Undo:
                UndoSearchEdit();
                break;
            case SearchEditCommandKind.MoveCaret:
                if (command.Value < 0)
                    MoveSearchCaretLeft(ctrl: true, shift: command.Shift);
                else
                    MoveSearchCaretRight(ctrl: true, shift: command.Shift);
                break;
            case SearchEditCommandKind.Delete:
                if (command.Value < 0)
                    DeleteSearchBackward(ctrl: true);
                else
                    DeleteSearchForward(ctrl: true);
                break;
        }
    }

    private void ApplyMainSelectionMovement(int delta, bool extendSelection)
    {
        if (extendSelection)
            MoveSelectionExtend(delta);
        else if (_selectionCursor.HasPointCursor)
            MoveKeyboardPointSelectionCursor(delta);
        else
            MoveSelection(delta);
    }

    private void ApplyHjklSelectionMovement(int delta, bool extendSelection)
    {
        if (extendSelection)
        {
            if (_selectionCursor.HasPointCursor)
                ExtendKeyboardPointSelectionCursor(delta);
            else
                MoveSelectionExtend(delta);
        }
        else if (_selectionCursor.HasPointCursor)
        {
            MoveKeyboardPointSelectionCursor(delta);
        }
        else
        {
            MoveSelection(delta);
        }
    }

    private void HandleMainEscapeKey()
    {
        if (BatchMenuPopup.IsOpen)
        {
            BatchMenuPopup.IsOpen = false;
            CloseBatchMenuNavUi();
            return;
        }
        if (ContextPopup.IsOpen)
        {
            CloseContextMenuPopup();
            return;
        }
        if (ShortcutHelpPopup.IsOpen)
        {
            ShortcutHelpPopup.IsOpen = false;
            return;
        }
        if (EntryPreviewPopup.IsOpen)
        {
            CloseEntryPreviewBubble();
            return;
        }
        if (_pendingDeleteEntry != null)
        {
            ClearPendingDelete();
            return;
        }

        if (_searchText.Length > 0)
            ClearSearchText();
        else
            HidePopup();
    }
}
