using System.Runtime.InteropServices;
using System.Windows;
using Brush = System.Windows.Media.Brush;

namespace ClipboardManager;

public partial class PopupWindow : Window
{
    private void UpdateSearchUI()
    {
        var hasSearch = _searchEditor.HasText;
        SearchBarPanel.Visibility = hasSearch ? Visibility.Visible : Visibility.Collapsed;
        SetCurrentValue(HighlightSearchQueryProperty, _searchText.Trim());
        if (hasSearch)
        {
            var primary = TryFindResource("PrimaryText") as Brush ?? System.Windows.Media.Brushes.White;
            var accent = TryFindResource("AccentBg") as Brush ?? System.Windows.Media.Brushes.Teal;
            SearchMouseController.Render(primary, accent);
        }
        else
        {
            SearchMouseController.ClearVisual();
        }

        SearchCountText.Text = hasSearch ? $"{_displayItems.Count} 条结果" : "";
    }

    private void ResetSearchEditorState()
    {
        EndSearchMouseSelection();
        _searchEditor.Reset();
    }

    private void ClearSearchText()
    {
        if (!_searchEditor.HasText) return;
        EndSearchMouseSelection();
        if (_searchEditor.Clear())
            RefreshFilterForSearchInput();
    }

    private void InsertSearchChar(char character)
    {
        _searchEditor.Insert(character);
        RefreshFilterForSearchInput();
    }

    private void MoveSearchCaretLeft(bool ctrl, bool shift)
    {
        _searchEditor.MoveCaretLeft(ctrl, shift);
        UpdateSearchUI();
    }

    private void MoveSearchCaretRight(bool ctrl, bool shift)
    {
        _searchEditor.MoveCaretRight(ctrl, shift);
        UpdateSearchUI();
    }

    private void DeleteSearchBackward(bool ctrl)
    {
        if (_searchEditor.DeleteBackward(ctrl))
            RefreshFilterForSearchInput();
    }

    private void DeleteSearchForward(bool ctrl)
    {
        if (_searchEditor.DeleteForward(ctrl))
            RefreshFilterForSearchInput();
    }

    private void MoveSearchCaret(int newIndex, bool extendSelection)
    {
        _searchEditor.MoveCaret(newIndex, extendSelection);
        UpdateSearchUI();
    }

    private void InsertPastedSearchText(string? text)
    {
        if (_searchEditor.InsertPastedText(text))
            RefreshFilterForSearchInput();
    }

    private async Task PasteSystemClipboardIntoSearchAsync()
    {
        for (var attempt = 0; attempt < 5 && _isPopupVisible; attempt++)
        {
            try
            {
                if (!System.Windows.Clipboard.ContainsText(System.Windows.TextDataFormat.UnicodeText)) return;
                InsertPastedSearchText(
                    System.Windows.Clipboard.GetText(System.Windows.TextDataFormat.UnicodeText));
                return;
            }
            catch (ExternalException)
            {
                if (attempt == 4) return;
                await Task.Delay(25);
            }
        }
    }

    private void SelectAllSearchText()
    {
        _searchEditor.SelectAll();
        UpdateSearchUI();
    }

    private async Task CopySearchSelectionAsync()
    {
        if (!TryGetSearchSelection(out var start, out var end)) return;
        await FastClipboardTextWriter.TrySetAsync(_hwnd, _searchText[start..end]);
    }

    private async Task CutSearchSelectionAsync()
    {
        if (!TryGetSearchSelection(out var start, out var end)) return;

        var expected = _searchEditor.Capture();
        var selectedText = _searchText[start..end];
        if (!await FastClipboardTextWriter.TrySetAsync(_hwnd, selectedText)) return;
        if (_searchEditor.Capture() != expected) return;

        if (_searchEditor.DeleteRange(start, end))
            RefreshFilterForSearchInput();
    }

    private void UndoSearchEdit()
    {
        if (_searchEditor.Undo())
            RefreshFilterForSearchInput();
    }

    private void RedoSearchEdit()
    {
        if (_searchEditor.Redo())
            RefreshFilterForSearchInput();
    }

    private bool TryGetSearchSelection(out int start, out int end) =>
        _searchEditor.TryGetSelection(out start, out end);

}
