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
    private void ClearPendingDelete()
    {
        if (_pendingDeleteEntry == null) return;
        _pendingDeleteEntry.IsPendingDelete = false;
        _pendingDeleteEntry = null;
    }

    private void ItemsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ItemsList.SelectedItem is ClipboardEntry sel && ReferenceEquals(sel, _pendingDeleteEntry))
            return;
        ClearPendingDelete();
        SyncEntryPreviewWithSelection();
    }

    private void ItemsList_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (e.VerticalChange == 0 || _displayItems.Count == 0) return;
        var sv = GetListScrollViewer();
        if (sv == null) return;

        double itemHeight = sv.ExtentHeight / _displayItems.Count;
        if (itemHeight <= 0) return;

        int newFirstVisible = Math.Max(0, (int)(sv.VerticalOffset / itemHeight));
        if (newFirstVisible == _firstVisibleIndex) return;

        // 仅同步「当前首行」与快速粘贴序号显示。勿在此处改写 SelectedIndex：
        // ↓ 键 + ScrollIntoView 会先更新选中再滚动，若按「旧首行 + 新选中」推算 relSelection，
        // 会把选中项错误推到 newFirstVisible + relSelection（例如从末行再下移一条时被甩到更下方）。
        _firstVisibleIndex = newFirstVisible;
        UpdateVisibleIndices(newFirstVisible);
    }

    private void ItemsList_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Middle)
        {
            if (e.OriginalSource is not DependencyObject srcM) return;
            if (ItemsList.ContainerFromElement(srcM) is not ListBoxItem lbiM || lbiM.DataContext is not ClipboardEntry entryM)
                return;

            ItemsList.SelectedItem = entryM;
            ItemsList.ScrollIntoView(entryM);
            ClearKeyboardPointSelectionCursor();
            var middleIndex = _displayItems.IndexOf(entryM);
            if (middleIndex >= 0)
                _selectionCursor.SetMouseAnchor(_displayItems.Count, middleIndex);
            ShowEntryPreviewBubble();
            e.Handled = true;
            return;
        }

        if (e.ChangedButton != MouseButton.Left) return;

        if (e.OriginalSource is not DependencyObject src) return;
        if (ItemsList.ContainerFromElement(src) is not ListBoxItem lbi || lbi.DataContext is not ClipboardEntry entry)
            return;

        int idx = _displayItems.IndexOf(entry);
        if (idx < 0) return;

        // 双击才粘贴：必须在 Preview 内处理；若此处已 Handled，冒泡阶段收不到 MouseLeftButtonDown，ClickCount 也无意义。
        if (_appSettings?.PasteRequiresDoubleClick == true && e.ClickCount == 2)
        {
            if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0 || (Keyboard.Modifiers & ModifierKeys.Control) != 0)
                return;
            ItemsList.SelectedItems.Clear();
            ItemsList.SelectedItems.Add(entry);
            _selectionCursor.SetMouseAnchor(_displayItems.Count, idx);
            ClearKeyboardPointSelectionCursor();
            e.Handled = true;
            PasteSelectedItem();
            return;
        }

        bool shift = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;
        bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) != 0;

        if (shift)
        {
            var range = _selectionCursor.SelectMouseRange(_displayItems.Count, idx, ItemsList.SelectedIndex);
            ReplaceSelectedIndices(range.Indices);
            SetKeyboardPointSelectionCursor(idx);
            e.Handled = true;
            return;
        }

        if (ctrl)
        {
            if (ItemsList.SelectedItems.Contains(entry))
                ItemsList.SelectedItems.Remove(entry);
            else
                ItemsList.SelectedItems.Add(entry);
            ItemsList.ScrollIntoView(entry);
            SetKeyboardPointSelectionCursor(idx);
            e.Handled = true;
            return;
        }

        ItemsList.SelectedItems.Clear();
        ItemsList.SelectedItems.Add(entry);
        _selectionCursor.SetMouseAnchor(_displayItems.Count, idx);
        ClearKeyboardPointSelectionCursor();
        e.Handled = true;
    }

    private void ItemsList_PreviewMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left) return;
        if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0 || (Keyboard.Modifiers & ModifierKeys.Control) != 0)
            return;
        if (ItemsList.SelectedItems.Count != 1) return;
        if (_appSettings?.PasteRequiresDoubleClick == true) return;
        if (e.OriginalSource is not DependencyObject srcUp) return;
        if (ItemsList.ContainerFromElement(srcUp) is ListBoxItem lbi && lbi.DataContext is ClipboardEntry sel)
        {
            ItemsList.SelectedItem = sel;
            PasteSelectedItem();
        }
    }

    private void ItemsList_PreviewMouseRightUp(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject srcR) return;
        if (ItemsList.ContainerFromElement(srcR) is ListBoxItem lbi && lbi.DataContext is ClipboardEntry entry)
        {
            SyncContextMenuForEntry(entry);
            ContextPopup.Placement = PlacementMode.Mouse;
            ContextPopup.PlacementTarget = null;
            _ctxAltCloseMenuArmed = false;
            ContextPopup.IsOpen = true;
            RebuildContextMenuNav();
            _contextNavIndex = 0;
            ApplyContextMenuHighlight();
            e.Handled = true;
        }
    }
}
