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
    private void TypeFilter_Click(object sender, MouseButtonEventArgs e) => CycleTypeFilter();

    private void CycleTypeFilter()
    {
        _quickPhraseOnly = false;
        _typeFilter = _typeFilter switch
        {
            null => EntryType.Text,
            EntryType.Text => EntryType.Image,
            EntryType.Image => EntryType.Files,
            _ => null
        };
        UpdateTypeFilterUi();
        RefreshFilter();
    }

    private void ToggleQuickPhraseFilter()
    {
        _quickPhraseOnly = !_quickPhraseOnly;
        if (_quickPhraseOnly)
        {
            _typeFilter = null;
        }
        UpdateTypeFilterUi();
        RefreshFilter();
    }

    private void UpdateTypeFilterUi()
    {
        (TypeFilterIcon.Text, TypeFilterText.Text) = _quickPhraseOnly
            ? ("⚡", "短语")
            : _typeFilter switch
            {
                EntryType.Text => ("T", "文本"),
                EntryType.Image => ("▣", "图片"),
                EntryType.Files => ("▰", "文件"),
                _ => ("●", "全部")
            };
    }

    private void PasteByIndex(int index)
    {
        var item = _displayItems.FirstOrDefault(i => i.DisplayIndex == index);
        if (item == null) return;
        ItemsList.SelectedItem = item;
        int li = _displayItems.IndexOf(item);
        if (li >= 0) _selectionCursor.SetMouseAnchor(_displayItems.Count, li);
        PasteSelectedItem();
    }
}
