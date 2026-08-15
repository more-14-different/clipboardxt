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
    private void OpenContextMenuFromKeyboard()
    {
        if (_displayItems.Count == 0) return;
        if (ItemsList.SelectedItem is not ClipboardEntry entry) return;

        SyncContextMenuForEntry(entry);
        RebuildContextMenuNav();
        if (_contextMenuNav.Count == 0) return;

        _contextNavIndex = 0;
        if (ItemsList.ItemContainerGenerator.ContainerFromItem(entry) is ListBoxItem li)
        {
            ContextPopup.PlacementTarget = li;
            ContextPopup.Placement = PlacementMode.Right;
            ContextPopup.HorizontalOffset = 8;
            ContextPopup.VerticalOffset = 0;
        }
        else
        {
            ContextPopup.PlacementTarget = MainBorder;
            ContextPopup.Placement = PlacementMode.Center;
            ContextPopup.HorizontalOffset = 0;
            ContextPopup.VerticalOffset = 0;
        }

        _ctxAltCloseMenuArmed = false;
        ContextPopup.IsOpen = true;
        ApplyContextMenuHighlight();
    }

    private void RebuildContextMenuNav()
    {
        _contextMenuNav.Clear();
        void Add(Border b, Action a)
        {
            if (b.Visibility == Visibility.Visible)
                _contextMenuNav.Add((b, a));
        }

        Add(CtxPasteBorder, ActivateCtxPaste);
        Add(CtxLinePasteBorder, ActivateCtxLinePaste);
        Add(CtxSoftLinePasteBorder, ActivateCtxSoftLinePaste);
        Add(CtxOpenUrlsBorder, ActivateCtxOpenUrls);
        Add(CtxPasteAsFileBorder, ActivateCtxPasteAsFile);
        Add(CtxPasteJsonFileBorder, ActivateCtxPasteJsonFile);
        Add(CtxEditTextBorder, ActivateCtxEditText);
        Add(CtxStarBorder, ActivateCtxStar);
        Add(CtxShortcutBorder, ActivateCtxShortcut);
        Add(CtxDeleteBorder, ActivateCtxDelete);
    }

    private void ApplyContextMenuHighlight()
    {
        var hi = FindResource("SelectedBrush") as Brush ?? System.Windows.Media.Brushes.LightGray;
        for (int i = 0; i < _contextMenuNav.Count; i++)
        {
            var row = _contextMenuNav[i].Row;
            if (i == _contextNavIndex)
                row.Background = hi;
            else
                row.ClearValue(Border.BackgroundProperty);
        }
    }

    private void MoveContextMenuHighlight(int delta)
    {
        if (_contextMenuNav.Count == 0) return;
        _contextNavIndex = (_contextNavIndex + delta + _contextMenuNav.Count) % _contextMenuNav.Count;
        ApplyContextMenuHighlight();
    }

    private void ActivateContextMenuHighlight()
    {
        if (_contextMenuNav.Count == 0) return;
        if (_contextNavIndex < 0 || _contextNavIndex >= _contextMenuNav.Count) return;
        _contextMenuNav[_contextNavIndex].Activate();
    }

    private void CloseContextMenuPopup()
    {
        foreach (var (row, _) in _contextMenuNav)
            row.ClearValue(Border.BackgroundProperty);
        _contextMenuNav.Clear();
        _contextNavIndex = 0;
        ContextPopup.IsOpen = false;
        _ctxAltCloseMenuArmed = false;
    }
}
