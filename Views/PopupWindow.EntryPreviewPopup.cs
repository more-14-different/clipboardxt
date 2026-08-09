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
    private void CloseEntryPreviewBubble()
    {
        EntryPreviewPopup.IsOpen = false;
        _previewImageFiles = null;
        _previewImageFileIndex = 0;
        _previewImageFilesSource = null;
    }

    private void ToggleEntryPreviewBubble()
    {
        if (_displayItems.Count == 0) return;
        if (ItemsList.SelectedItem is not ClipboardEntry) return;

        if (EntryPreviewPopup.IsOpen)
        {
            CloseEntryPreviewBubble();
            return;
        }

        ShowEntryPreviewBubble();
    }

    /// <summary>打开预览气泡（不关闭已打开的预览；用于中键切换条目时更新内容）。</summary>
    private void ShowEntryPreviewBubble()
    {
        if (_displayItems.Count == 0) return;
        if (ItemsList.SelectedItem is not ClipboardEntry entry) return;

        UpdateEntryPreviewBubbleContent(entry);
        EntryPreviewPopup.IsOpen = true;
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
        {
            if (EntryPreviewPopup.IsOpen)
                PositionEntryPreviewPopup();
        });
    }

    private void PositionEntryPreviewPopup()
    {
        if (MainBorder.ActualWidth <= 0 || !IsVisible)
        {
            EntryPreviewPopup.PlacementTarget = MainBorder;
            EntryPreviewPopup.Placement = PlacementMode.Right;
            EntryPreviewPopup.HorizontalOffset = 10;
            EntryPreviewPopup.VerticalOffset = 0;
            return;
        }

        const double gap = 10;

        // 开始菜单/搜索等 Shell 前台：主面板固定在屏幕一侧，预览改到主窗口正下方，避免横向弹出压住开始菜单。
        if (IsShellForegroundWindow(Win32.GetForegroundWindow()))
        {
            EntryPreviewPopup.PlacementTarget = MainBorder;
            EntryPreviewPopup.Placement = PlacementMode.Bottom;
            EntryPreviewPopup.HorizontalOffset = 0;
            EntryPreviewPopup.VerticalOffset = gap;
            return;
        }

        const double previewNominalW = 548;
        const double previewNominalH = 420;

        try
        {
            EntryPreviewPopup.Child?.Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
        }
        catch
        {
            /* ignore */
        }

        var desiredW = EntryPreviewPopup.Child is FrameworkElement fe && fe.DesiredSize.Width > 1
            ? fe.DesiredSize.Width
            : previewNominalW;

        var topLeft = MainBorder.PointToScreen(new System.Windows.Point(0, 0));
        double mbW = MainBorder.ActualWidth;
        double mbH = MainBorder.ActualHeight;
        double mainRightEdge = topLeft.X + mbW;
        double mainLeftEdge = topLeft.X;

        var wa = System.Windows.Forms.Screen.FromPoint(
            new System.Drawing.Point(
                (int)(topLeft.X + mbW / 2),
                (int)(topLeft.Y + mbH / 2))).WorkingArea;

        double spaceRight = wa.Right - mainRightEdge;
        double spaceLeft = mainLeftEdge - wa.Left;
        bool placeRight = spaceRight >= desiredW + gap || spaceRight >= spaceLeft;

        // 相对选中行略偏上（数值可按观感微调）
        const double previewVerticalNudgeUp = 24;

        ListBoxItem? selectedRow = null;
        if (ItemsList.SelectedItem != null
            && ItemsList.ItemContainerGenerator.ContainerFromItem(ItemsList.SelectedItem) is ListBoxItem row
            && row.IsVisible)
            selectedRow = row;

        // 右侧有空间时：以选中行容器为锚点。Popup.Right 的目标原点为「行首右上角」，预览顶与行顶对齐；
        // 若仍用 MainBorder 顶边 + (itemTop - topLeft)，在列表中部选中时易与触边重算叠加，观感像对齐到行中部。
        if (placeRight && selectedRow != null)
        {
            EntryPreviewPopup.PlacementTarget = selectedRow;
            EntryPreviewPopup.Placement = PlacementMode.Right;
            EntryPreviewPopup.HorizontalOffset = gap;
            EntryPreviewPopup.VerticalOffset = -previewVerticalNudgeUp;
            return;
        }

        EntryPreviewPopup.PlacementTarget = MainBorder;
        if (placeRight)
        {
            EntryPreviewPopup.Placement = PlacementMode.Right;
            EntryPreviewPopup.HorizontalOffset = gap;
        }
        else
        {
            EntryPreviewPopup.Placement = PlacementMode.Left;
            EntryPreviewPopup.HorizontalOffset = -gap;
        }

        double verticalOffset = 0;
        if (selectedRow != null)
        {
            var itemTop = selectedRow.PointToScreen(new System.Windows.Point(0, 0));
            verticalOffset = itemTop.Y - topLeft.Y - previewVerticalNudgeUp;
        }

        double minOff = wa.Top + 8 - topLeft.Y;
        double maxOff = wa.Bottom - 8 - previewNominalH - topLeft.Y;
        if (maxOff < minOff) maxOff = minOff;
        verticalOffset = Math.Clamp(verticalOffset, minOff, maxOff);

        EntryPreviewPopup.VerticalOffset = verticalOffset;
    }

    private void SyncEntryPreviewWithSelection()
    {
        if (!EntryPreviewPopup.IsOpen) return;
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
        {
            if (!EntryPreviewPopup.IsOpen) return;
            if (ItemsList.SelectedItem is ClipboardEntry e)
            {
                UpdateEntryPreviewBubbleContent(e);
                PositionEntryPreviewPopup();
            }
            else
                CloseEntryPreviewBubble();
        });
    }
}
