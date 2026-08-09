using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Brush = System.Windows.Media.Brush;
using Button = System.Windows.Controls.Button;
using Orientation = System.Windows.Controls.Orientation;
using TextBox = System.Windows.Controls.TextBox;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using ClipboardManager.Models;

namespace ClipboardManager;

public partial class PopupWindow : Window
{
    private void MoveSelection(int delta)
    {
        if (_displayItems.Count == 0) return;
        ClearKeyboardPointSelectionCursor();
        var idx = _selectionCursor.MoveSingle(_displayItems.Count, ItemsList.SelectedIndex, delta);
        ItemsList.SelectedIndex = idx;
        ItemsList.ScrollIntoView(ItemsList.SelectedItem);
    }

    private void MoveSelectionExtend(int delta)
    {
        if (_displayItems.Count == 0) return;
        ClearKeyboardPointSelectionCursor();

        var range = _selectionCursor.ExtendRange(_displayItems.Count, ItemsList.SelectedIndex, delta);
        ReplaceSelectedIndices(range.Indices);
        ScrollIndexIntoView(range.FocusIndex);
    }

    private void MoveSelectionToFirst()
    {
        if (_displayItems.Count == 0) return;
        ClearKeyboardPointSelectionCursor();
        ItemsList.SelectedIndex = _selectionCursor.MoveSingleTo(_displayItems.Count, 0);
        ItemsList.ScrollIntoView(ItemsList.SelectedItem);
    }

    private void MoveSelectionToLast()
    {
        if (_displayItems.Count == 0) return;
        ClearKeyboardPointSelectionCursor();
        ItemsList.SelectedIndex = _selectionCursor.MoveSingleTo(_displayItems.Count, _displayItems.Count - 1);
        ItemsList.ScrollIntoView(ItemsList.SelectedItem);
    }

    private void ScrollPage(int direction)
    {
        if (_displayItems.Count == 0) return;
        ClearKeyboardPointSelectionCursor();
        var sv = GetListScrollViewer();
        if (sv == null) return;

        double itemHeight = sv.ExtentHeight / _displayItems.Count;
        if (itemHeight <= 0) return;

        int oldFirstVisible = Math.Max(0, (int)(sv.VerticalOffset / itemHeight));
        int relSelection = Math.Max(0, ItemsList.SelectedIndex - oldFirstVisible);

        double newOffset = sv.VerticalOffset + direction * _pageSize * itemHeight;
        newOffset = Math.Max(0, Math.Min(newOffset, sv.ScrollableHeight));
        sv.ScrollToVerticalOffset(newOffset);

        int newFirstVisible = Math.Max(0, (int)(newOffset / itemHeight));
        _firstVisibleIndex = newFirstVisible;
        int newSel = Math.Clamp(newFirstVisible + relSelection, 0, _displayItems.Count - 1);
        ItemsList.SelectedIndex = _selectionCursor.MoveSingleTo(_displayItems.Count, newSel);

        UpdateVisibleIndices(newFirstVisible);
    }

    private void ScrollIndexIntoView(int index)
    {
        if (index >= 0 && index < _displayItems.Count)
            ItemsList.ScrollIntoView(_displayItems[index]);
    }

    private void UpdateVisibleIndices(int firstVisible)
    {
        for (int i = 0; i < _displayItems.Count; i++)
        {
            int rel = i - firstVisible + 1;
            _displayItems[i].DisplayIndex = (rel >= 1 && rel <= 9) ? rel : 0;
        }
    }

    private ScrollViewer? GetListScrollViewer()
    {
        if (VisualTreeHelper.GetChildrenCount(ItemsList) == 0) return null;
        var border = VisualTreeHelper.GetChild(ItemsList, 0) as System.Windows.Controls.Decorator;
        return border?.Child as ScrollViewer;
    }
}
