using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using ClipboardManager.Models;
using Media = System.Windows.Media;
using Orientation = System.Windows.Controls.Orientation;

namespace ClipboardManager;

public partial class FileDialogJumpPickerWindow : Window
{    private void ItemsList_OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left) return;
        if (FindAncestorListBoxItem(e.OriginalSource as DependencyObject) is { } lbi
            && lbi.DataContext is FileJumpPickerRow row)
        {
            ItemsList.SelectedItem = row;
            bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
            CommitSelection(row.Path, pasteText: ctrl);
        }
    }

    private void ItemsList_PreviewMouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (FindAncestorListBoxItem(e.OriginalSource as DependencyObject) is { } lbi
            && lbi.DataContext is FileJumpPickerRow row)
            ItemsList.SelectedItem = row;
    }

    /// <summary>
    /// 沿可视/逻辑树向上找最近的 <see cref="ListBoxItem"/>。
    /// 注意：原 <c>OriginalSource</c> 可能是 <see cref="System.Windows.Documents.Run"/> 等
    /// <see cref="ContentElement"/>（来自高亮 TextBlock 的 Inlines），它不是 Visual，
    /// 直接调用 <see cref="VisualTreeHelper.GetParent"/> 会抛
    /// "System.Windows.Documents.Run 不是 Visual 或 Visual3D"。
    /// 因此需要先用 <see cref="LogicalTreeHelper"/> 走到 Visual 节点再切回视觉树。
    /// </summary>
    private static ListBoxItem? FindAncestorListBoxItem(DependencyObject? start)
    {
        var el = start;
        while (el != null && el is not ListBoxItem)
        {
            DependencyObject? parent = null;
            if (el is Visual or System.Windows.Media.Media3D.Visual3D)
            {
                try { parent = VisualTreeHelper.GetParent(el); }
                catch { parent = null; }
            }

            parent ??= LogicalTreeHelper.GetParent(el);
            if (parent == null) return null;
            el = parent;
        }

        return el as ListBoxItem;
    }

    private enum FileJumpPickerFilterMode
    {
        All,
        FavoritesOnly,
        RecentOnly
    }

    private void JumpByQuickIndex(int index1To9)
    {
        var row = _displayRows.FirstOrDefault(r => r.DisplayIndex == index1To9);
        if (row != null)
            CommitSelection(row.Path);
    }

    private void MoveSelection(int delta)
    {
        if (_displayRows.Count == 0) return;
        var idx = ItemsList.SelectedIndex + delta;
        if (idx < 0) idx = 0;
        if (idx >= _displayRows.Count) idx = _displayRows.Count - 1;
        ItemsList.SelectedIndex = idx;
        ItemsList.ScrollIntoView(ItemsList.SelectedItem);
        SyncJumpPreviewWithSelection();
    }

    private void MoveSelectionToBoundary(bool moveToEnd)
    {
        if (_displayRows.Count == 0) return;
        ItemsList.SelectedIndex = moveToEnd ? _displayRows.Count - 1 : 0;
        ItemsList.ScrollIntoView(ItemsList.SelectedItem);
        SyncJumpPreviewWithSelection();
    }

    private void ScrollPage(int direction)
    {
        if (_displayRows.Count == 0) return;
        var sv = GetListScrollViewer();
        if (sv == null) return;

        double itemHeight = sv.ExtentHeight / _displayRows.Count;
        if (itemHeight <= 0) return;

        int oldFirstVisible = Math.Max(0, (int)(sv.VerticalOffset / itemHeight));
        int relSelection = Math.Max(0, ItemsList.SelectedIndex - oldFirstVisible);

        double newOffset = sv.VerticalOffset + direction * PageSize * itemHeight;
        newOffset = Math.Max(0, Math.Min(newOffset, sv.ScrollableHeight));
        sv.ScrollToVerticalOffset(newOffset);

        int newFirstVisible = Math.Max(0, (int)(newOffset / itemHeight));
        _firstVisibleIndex = newFirstVisible;
        int newSel = Math.Clamp(newFirstVisible + relSelection, 0, _displayRows.Count - 1);
        ItemsList.SelectedIndex = newSel;

        AssignVisibleQuickIndices(newFirstVisible);
        SyncJumpPreviewWithSelection();
    }

    private ScrollViewer? GetListScrollViewer()
    {
        if (VisualTreeHelper.GetChildrenCount(ItemsList) == 0) return null;
        var border = VisualTreeHelper.GetChild(ItemsList, 0) as Decorator;
        return border?.Child as ScrollViewer;
    }

    private void ItemsList_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (e.VerticalChange == 0 || _displayRows.Count == 0) return;
        var sv = GetListScrollViewer();
        if (sv == null) return;

        double itemHeight = sv.ExtentHeight / _displayRows.Count;
        if (itemHeight <= 0) return;

        int newFirstVisible = Math.Max(0, (int)(sv.VerticalOffset / itemHeight));
        if (newFirstVisible == _firstVisibleIndex) return;

        int relSelection = Math.Max(0, ItemsList.SelectedIndex - _firstVisibleIndex);
        _firstVisibleIndex = newFirstVisible;

        int newSel = Math.Clamp(newFirstVisible + relSelection, 0, _displayRows.Count - 1);
        ItemsList.SelectedIndex = newSel;

        AssignVisibleQuickIndices(newFirstVisible);
        SyncJumpPreviewWithSelection();
    }
}

