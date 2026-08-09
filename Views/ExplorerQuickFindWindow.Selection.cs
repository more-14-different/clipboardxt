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
}
