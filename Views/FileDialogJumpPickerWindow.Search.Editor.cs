using System.Runtime.InteropServices;
using System.Windows;
using Media = System.Windows.Media;

namespace ClipboardManager;

public partial class FileDialogJumpPickerWindow : Window
{
    private void UpdateSearchChrome()
    {
        var hasSearch = _searchEditor.HasText;
        SearchBarPanel.Visibility = hasSearch ? Visibility.Visible : Visibility.Collapsed;
        SetCurrentValue(HighlightSearchQueryProperty, _searchText.Trim());
        if (hasSearch)
        {
            var primary = TryFindResource("PrimaryText") as Media.Brush ?? Media.Brushes.White;
            var accent = TryFindResource("AccentBg") as Media.Brush ?? Media.Brushes.Teal;
            SearchMouseController.Render(primary, accent);
        }
        else
        {
            SearchMouseController.ClearVisual();
        }

        SearchCountText.Text = hasSearch ? $"{_displayRows.Count} 条结果" : "";
    }

    private void ClearSearchText()
    {
        if (!_searchEditor.HasText) return;
        EndSearchMouseSelection();
        if (_searchEditor.Clear())
            RefreshAfterSearchTextEdit();
    }

    private void ResetSearchEditorState()
    {
        EndSearchMouseSelection();
        _searchEditor.Reset();
        _hasSearchText = false;
    }

    private void InsertSearchChar(char character)
    {
        _searchEditor.Insert(character);
        RefreshAfterSearchTextEdit();
    }

    internal void InsertPastedSearchText(string? text)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(() => InsertPastedSearchText(text));
            return;
        }
        if (!CanReceiveSearchPaste) return;

        if (_searchEditor.InsertPastedText(text))
            RefreshAfterSearchTextEdit();
    }

    internal bool TryGetSearchPasteScreenAnchor(out System.Windows.Point anchor)
    {
        anchor = default;
        if (!CanReceiveSearchPaste || PresentationSource.FromVisual(this) == null) return false;

        try
        {
            if (SearchBarPanel.IsVisible && SearchTextBlock.ActualWidth > 0)
            {
                anchor = SearchTextBlock.PointToScreen(
                    new System.Windows.Point(0, Math.Max(SearchTextBlock.ActualHeight, 16)));
                return true;
            }

            // 空查询时搜索栏为 Collapsed；用标题栏底部推算它展开后的文本基线位置。
            // 横向偏移对应：外边距 8 + 内边距 10 + 搜索图标约 20。
            var mainTopLeft = MainBorder.PointToScreen(new System.Windows.Point(0, 0));
            var headerBottom = FileJumpHeaderPanel.PointToScreen(
                new System.Windows.Point(0, FileJumpHeaderPanel.ActualHeight));
            anchor = new System.Windows.Point(mainTopLeft.X + 38, headerBottom.Y + 35);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private async Task PasteSystemClipboardIntoSearchAsync()
    {
        for (var attempt = 0; attempt < 5 && CanReceiveSearchPaste; attempt++)
        {
            try
            {
                if (!System.Windows.Clipboard.ContainsText(System.Windows.TextDataFormat.UnicodeText)) return;
                InsertPastedSearchText(System.Windows.Clipboard.GetText(System.Windows.TextDataFormat.UnicodeText));
                return;
            }
            catch (ExternalException)
            {
                if (attempt == 4) return;
                await Task.Delay(25);
            }
        }
    }

    private void MoveSearchCaretLeft(bool ctrl, bool shift)
    {
        _searchEditor.MoveCaretLeft(ctrl, shift);
        UpdateSearchChrome();
    }

    private void MoveSearchCaretRight(bool ctrl, bool shift)
    {
        _searchEditor.MoveCaretRight(ctrl, shift);
        UpdateSearchChrome();
    }

    private void DeleteSearchBackward(bool ctrl)
    {
        if (_searchEditor.DeleteBackward(ctrl))
            RefreshAfterSearchTextEdit();
    }

    private void DeleteSearchForward(bool ctrl)
    {
        if (_searchEditor.DeleteForward(ctrl))
            RefreshAfterSearchTextEdit();
    }

    private void MoveSearchCaret(int newIndex, bool extendSelection)
    {
        _searchEditor.MoveCaret(newIndex, extendSelection);
        UpdateSearchChrome();
    }

    private void SelectAllSearchText()
    {
        _searchEditor.SelectAll();
        UpdateSearchChrome();
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
            RefreshAfterSearchTextEdit();
    }

    private void UndoSearchEdit()
    {
        if (_searchEditor.Undo())
            RefreshAfterSearchTextEdit();
    }

    private void RedoSearchEdit()
    {
        if (_searchEditor.Redo())
            RefreshAfterSearchTextEdit();
    }

    private bool TryGetSearchSelection(out int start, out int end) =>
        _searchEditor.TryGetSelection(out start, out end);

    private void RefreshAfterSearchTextEdit()
    {
        _hasSearchText = _searchEditor.HasText;
        ScheduleSearchRefresh();
    }

    private static bool IsPhysicalShiftDown() =>
        (Win32.GetAsyncKeyState(0x10) & 0x8000) != 0
        || (Win32.GetAsyncKeyState(0xA0) & 0x8000) != 0
        || (Win32.GetAsyncKeyState(0xA1) & 0x8000) != 0;

}
