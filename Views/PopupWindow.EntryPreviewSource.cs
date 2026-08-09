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
    private void UpdateEntryPreviewSource(ClipboardEntry entry)
    {
        var source = entry.Source;
        if (source == null || !source.HasAny) return;

        EntryPreviewSourcePanel.Visibility = Visibility.Visible;
        EntryPreviewSourceIcon.Source = entry.SourceAppIcon;
        SetEntryPreviewSourceText(EntryPreviewSourceApp, source.DisplayName, 12, FontWeights.SemiBold);
        SetEntryPreviewSourceText(EntryPreviewSourceTitle, source.WindowTitle ?? "");
        SetEntryPreviewSourceText(EntryPreviewSourceExe, source.ExeName ?? "");
        SetEntryPreviewSourceText(EntryPreviewSourceClass, source.WindowClass ?? "");
        SetEntryPreviewSourceText(EntryPreviewSourceFocus, source.FocusedClass ?? "");
        SetEntryPreviewSourceText(EntryPreviewSourceMethod, source.CaptureMethod ?? "");

        if (!string.IsNullOrWhiteSpace(source.ExePath))
        {
            SetEntryPreviewSourceText(EntryPreviewSourcePath, source.ExePath);
            EntryPreviewSourcePathRow.Visibility = Visibility.Visible;
        }
    }

    private void SetEntryPreviewSourceText(
        TextBlock textBlock,
        string? text,
        double fontSize = 10,
        FontWeight fontWeight = default)
    {
        var normal = textBlock.Foreground;
        var accent = TryFindResource("AccentBg") as Brush ?? System.Windows.Media.Brushes.Teal;
        textBlock.Inlines.Clear();
        SearchHighlightInlines.Append(
            textBlock.Inlines,
            text ?? "",
            _searchText.Trim(),
            normal,
            accent,
            fontSize,
            fontWeight == default ? FontWeights.Normal : fontWeight);
    }
}
