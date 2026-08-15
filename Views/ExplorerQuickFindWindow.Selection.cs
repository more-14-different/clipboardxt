using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Brush = System.Windows.Media.Brush;

namespace ClipboardManager;

public partial class ExplorerQuickFindWindow : Window
{
    public void MoveSelection(int delta)
    {
        if (ResultsList.Items.Count == 0) return;
        var i = ResultsList.SelectedIndex;
        if (i < 0) i = 0;
        i = Math.Clamp(i + delta, 0, ResultsList.Items.Count - 1);
        ResultsList.SelectedIndex = i;
        ResultsList.ScrollIntoView(ResultsList.SelectedItem);
    }

    public void MoveSelectionPage(int direction)
    {
        if (ResultsList.Items.Count == 0) return;
        const int pageSize = 8;
        MoveSelection(direction * pageSize);
    }

    public void MoveSelectionToEnd(bool last)
    {
        if (ResultsList.Items.Count == 0) return;
        ResultsList.SelectedIndex = last ? ResultsList.Items.Count - 1 : 0;
        ResultsList.ScrollIntoView(ResultsList.SelectedItem);
    }

    public string? GetSelectedFullPath()
    {
        if (ResultsList.SelectedItem is ListBoxItem { Tag: string s } && !string.IsNullOrEmpty(s))
            return s;
        return null;
    }

    public string? GetFullPathByIndex(int index)
    {
        if (index < 0 || index >= ResultsList.Items.Count) return null;
        if (ResultsList.Items[index] is ListBoxItem { Tag: string s } && !string.IsNullOrEmpty(s))
            return s;
        return null;
    }

    private void ResultsList_OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        var path = GetSelectedFullPath();
        if (!string.IsNullOrEmpty(path))
            ItemActivated?.Invoke(path!);
    }

    private void ResultsList_OnPreviewMouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (FindResultListBoxItem(e.OriginalSource as DependencyObject) is { } item)
        {
            ResultsList.SelectedItem = item;
            e.Handled = true;
            ResultContextMenu.PlacementTarget = item;
            ResultContextMenu.IsOpen = true;
        }
    }

    private static ListBoxItem? FindResultListBoxItem(DependencyObject? start)
    {
        var element = start;
        while (element != null && element is not ListBoxItem)
        {
            DependencyObject? parent = null;
            if (element is Visual or System.Windows.Media.Media3D.Visual3D)
            {
                try { parent = VisualTreeHelper.GetParent(element); }
                catch { parent = null; }
            }

            parent ??= LogicalTreeHelper.GetParent(element);
            element = parent;
        }

        return element as ListBoxItem;
    }

    private void ResultContextMenu_Opened(object sender, RoutedEventArgs e)
    {
        CtxActivateResult.IsEnabled = !string.IsNullOrEmpty(GetSelectedFullPath());
        CtxActivateResult.Header = UiLanguage.T(
            _settings?.ExplorerQuickFindOpenMode == "DirectOpen"
                ? "↗ 直接打开"
                : "⌖ 在资源管理器中定位");
    }

    private void CtxActivateResult_Click(object sender, RoutedEventArgs e)
    {
        var path = GetSelectedFullPath();
        if (!string.IsNullOrEmpty(path))
            ItemActivated?.Invoke(path);
    }
}
