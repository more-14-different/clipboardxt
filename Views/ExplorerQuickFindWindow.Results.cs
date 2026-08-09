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
    private void CacheBrushes()
    {
        _primaryBrush ??= (Brush)FindResource("PrimaryText");
        _secondaryBrush ??= (Brush)FindResource("SecondaryText");
        _mutedBrush ??= (Brush)FindResource("MutedText");
        if (_highlightBrush == null)
        {
            try
            {
                _highlightBrush = (Brush)FindResource("AccentBg");
            }
            catch
            {
                _highlightBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x13, 0x94, 0x93));
            }
        }
    }

    public void SetQueryText(string folderLabel, string typing, string? highlightNeedle = null)
    {
        FolderLabel.Text = folderLabel;
        TypingLabel.Inlines.Clear();
        if (string.IsNullOrEmpty(typing))
        {
            TypingLabel.Inlines.Add(new Run(" "));
            return;
        }

        CacheBrushes();
        SearchHighlightInlines.Append(
            TypingLabel.Inlines,
            typing,
            highlightNeedle ?? typing,
            _primaryBrush!,
            _highlightBrush!,
            14,
            FontWeights.SemiBold);
    }

    public void SetResults(
        IReadOnlyList<QuickFindResultItem> items,
        string? status,
        string? countLine = null,
        string? highlightNeedle = null)
    {
        CacheBrushes();
        var primary = _primaryBrush!;
        var secondary = _secondaryBrush!;
        var muted = _mutedBrush!;
        var highlight = _highlightBrush!;

        ResultsList.Items.Clear();

        for (int idx = 0; idx < items.Count; idx++)
        {
            var item = items[idx];

            var tb = new TextBlock
            {
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center,
            };

            if (idx < 9)
                tb.Inlines.Add(new Run($"{idx + 1} ") { Foreground = muted, FontSize = 10 });

            tb.Inlines.Add(new Run(item.IsDirectory ? "\uD83D\uDCC1 " : "\uD83D\uDCC4 ") { FontSize = 12 });
            SearchHighlightInlines.Append(tb.Inlines, item.FileName, highlightNeedle, primary, highlight, 13, FontWeights.Normal);

            if (!string.IsNullOrEmpty(item.RelativePath)
                && !string.Equals(item.RelativePath, item.FileName, StringComparison.OrdinalIgnoreCase))
            {
                tb.Inlines.Add(new Run("  ") { Foreground = secondary, FontSize = 11 });
                SearchHighlightInlines.Append(tb.Inlines, item.RelativePath, highlightNeedle, secondary, highlight, 11, FontWeights.Normal);
            }

            if (item.IsGlobalMatch)
                tb.Inlines.Add(new Run("  · 全盘") { Foreground = muted, FontSize = 10 });

            ResultsList.Items.Add(new ListBoxItem
            {
                Content = tb,
                ToolTip = item.FullPath,
                Tag = item.FullPath,
            });
        }

        if (ResultsList.Items.Count > 0)
            ResultsList.SelectedIndex = 0;

        CountLabel.Text = items.Count > 0
            ? (countLine ?? $"{items.Count} 项")
            : "";
        HintLabel.Text = string.IsNullOrEmpty(status) ? DefaultHint : status;
    }
}
