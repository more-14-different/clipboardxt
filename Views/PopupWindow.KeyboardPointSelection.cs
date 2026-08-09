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
    private void ToggleKeyboardPointSelection()
    {
        if (_displayItems.Count == 0) return;

        var result = _selectionCursor.TogglePoint(
            _displayItems.Count,
            ItemsList.SelectedIndex,
            GetSelectedIndices());
        ApplyPointSelectionResult(result);
    }

    private void ExtendKeyboardPointSelectionCursor(int delta)
    {
        if (_displayItems.Count == 0) return;

        var result = _selectionCursor.ExtendPoint(_displayItems.Count, ItemsList.SelectedIndex, delta);
        ApplyPointSelectionResult(result);
    }

    private void MoveKeyboardPointSelectionCursor(int delta)
    {
        if (_displayItems.Count == 0) return;
        SetKeyboardPointSelectionCursor(_selectionCursor.MovePoint(_displayItems.Count, ItemsList.SelectedIndex, delta));
    }

    private void MoveKeyboardPointSelectionCursorTo(int index)
    {
        if (_displayItems.Count == 0) return;
        SetKeyboardPointSelectionCursor(_selectionCursor.MovePointTo(_displayItems.Count, index));
    }

    private void ScrollKeyboardPointSelectionCursorPage(int direction)
    {
        if (_displayItems.Count == 0) return;
        var sv = GetListScrollViewer();
        if (sv == null)
        {
            MoveKeyboardPointSelectionCursor(direction * _pageSize);
            return;
        }

        double itemHeight = sv.ExtentHeight / _displayItems.Count;
        if (itemHeight <= 0)
        {
            MoveKeyboardPointSelectionCursor(direction * _pageSize);
            return;
        }

        double newOffset = sv.VerticalOffset + direction * _pageSize * itemHeight;
        newOffset = Math.Max(0, Math.Min(newOffset, sv.ScrollableHeight));
        sv.ScrollToVerticalOffset(newOffset);
        int newFirstVisible = Math.Max(0, (int)(newOffset / itemHeight));
        _firstVisibleIndex = newFirstVisible;
        MoveKeyboardPointSelectionCursor(direction * _pageSize);
        UpdateVisibleIndices(newFirstVisible);
    }

    private void SetKeyboardPointSelectionCursor(int index)
    {
        if (_displayItems.Count == 0)
        {
            ClearKeyboardPointSelectionCursor();
            return;
        }

        var next = Math.Clamp(index, 0, _displayItems.Count - 1);
        _selectionCursor.SetPointCursor(_displayItems.Count, next);
        if (_keyboardPointFocusEntry != null && !ReferenceEquals(_keyboardPointFocusEntry, _displayItems[next]))
            _keyboardPointFocusEntry.IsKeyboardPointFocus = false;

        _keyboardPointFocusEntry = _displayItems[next];
        _keyboardPointFocusEntry.IsKeyboardPointFocus = true;
        FocusKeyboardPointSelectionCursor();
    }

    private void ClearKeyboardPointSelectionCursor()
    {
        if (_keyboardPointFocusEntry != null)
            _keyboardPointFocusEntry.IsKeyboardPointFocus = false;
        _keyboardPointFocusEntry = null;
        _selectionCursor.ClearPointCursor();
    }

    private void FocusKeyboardPointSelectionCursor()
    {
        if (!_selectionCursor.HasPointCursor || _selectionCursor.PointCursor >= _displayItems.Count) return;
        var entry = _displayItems[_selectionCursor.PointCursor];
        ItemsList.ScrollIntoView(entry);
        ItemsList.UpdateLayout();
        if (ItemsList.ItemContainerGenerator.ContainerFromItem(entry) is ListBoxItem lbi)
            lbi.Focus();
    }

    private HashSet<int> GetSelectedIndices() =>
        ItemsList.SelectedItems.Cast<ClipboardEntry>()
            .Select(entry => _displayItems.IndexOf(entry))
            .Where(index => index >= 0)
            .ToHashSet();

    private void ReplaceSelectedIndices(IEnumerable<int> indices)
    {
        ItemsList.SelectedItems.Clear();
        foreach (var index in indices)
        {
            if (index >= 0 && index < _displayItems.Count)
                ItemsList.SelectedItems.Add(_displayItems[index]);
        }
    }

    private void ApplyPointSelectionResult(PointSelectionResult result)
    {
        foreach (var index in result.RemoveIndices)
        {
            if (index >= 0 && index < _displayItems.Count)
                ItemsList.SelectedItems.Remove(_displayItems[index]);
        }
        foreach (var index in result.AddIndices)
        {
            if (index >= 0 && index < _displayItems.Count
                && !ItemsList.SelectedItems.Contains(_displayItems[index]))
            {
                ItemsList.SelectedItems.Add(_displayItems[index]);
            }
        }
        SetKeyboardPointSelectionCursor(result.PointCursorIndex);
    }
}
