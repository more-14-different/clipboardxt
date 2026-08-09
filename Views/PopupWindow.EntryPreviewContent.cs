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
    private void UpdateEntryPreviewBubbleContent(ClipboardEntry? entry = null)
    {
        entry ??= ItemsList.SelectedItem as ClipboardEntry;

        EntryPreviewText.Visibility = Visibility.Collapsed;
        EntryPreviewImage.Visibility = Visibility.Collapsed;
        EntryPreviewImage.Source = null;
        EntryPreviewText.Inlines.Clear();
        EntryPreviewImageMeta.Visibility = Visibility.Collapsed;
        EntryPreviewImageMeta.Text = "";
        EntryPreviewImageNav.Visibility = Visibility.Collapsed;
        EntryPreviewOcrSeparator.Visibility = Visibility.Collapsed;
        EntryPreviewOcrHeader.Visibility = Visibility.Collapsed;
        EntryPreviewOcrText.Visibility = Visibility.Collapsed;
        EntryPreviewOcrText.Text = "";
        EntryPreviewShortcutPhraseRow.Visibility = Visibility.Collapsed;
        EntryPreviewShortcutPhraseChip.Content = null;
        EntryPreviewSourcePanel.Visibility = Visibility.Collapsed;
        EntryPreviewSourceIcon.Source = null;
        ClearEntryPreviewTextBlock(EntryPreviewSourceApp);
        ClearEntryPreviewTextBlock(EntryPreviewSourceTitle);
        ClearEntryPreviewTextBlock(EntryPreviewSourcePath);
        EntryPreviewSourcePathRow.Visibility = Visibility.Collapsed;
        ClearEntryPreviewTextBlock(EntryPreviewSourceExe);
        ClearEntryPreviewTextBlock(EntryPreviewSourceClass);
        ClearEntryPreviewTextBlock(EntryPreviewSourceFocus);
        ClearEntryPreviewTextBlock(EntryPreviewSourceMethod);

        if (_previewImageFiles == null || entry == null || !ReferenceEquals(_previewImageFilesSource, entry))
        {
            _previewImageFiles = null;
            _previewImageFileIndex = 0;
            _previewImageFilesSource = entry;
        }

        if (entry == null) return;
        UpdateEntryPreviewShortcutPhrase(entry);
        UpdateEntryPreviewSource(entry);

        if (entry.Type == EntryType.Text)
        {
            SetEntryPreviewText(string.IsNullOrEmpty(entry.TextContent)
                ? "（空文本）"
                : entry.TextContent);
            EntryPreviewText.Visibility = Visibility.Visible;
            return;
        }

        if (entry.Type == EntryType.Image || entry.IsImageFile)
        {
            if (entry.Type == EntryType.Image)
            {
                EntryPreviewImageMeta.Text = $"{entry.ImageWidth}×{entry.ImageHeight} 图片";
                EntryPreviewImageMeta.Visibility = Visibility.Visible;
            }
            else if (entry.IsMultiImageFiles)
            {
                _previewImageFiles = entry.GetImageFilePaths();
                if (_previewImageFiles.Length > 0)
                {
                    _previewImageFileIndex %= _previewImageFiles.Length;
                    EntryPreviewImageMeta.Text = $"共 {entry.FilePaths!.Length} 个文件 · {_previewImageFiles.Length} 张图片";
                    EntryPreviewImageMeta.Visibility = Visibility.Visible;
                }
            }
            else
            {
                EntryPreviewImageMeta.Text = $"{entry.FilePaths!.Length} 个文件";
                EntryPreviewImageMeta.Visibility = Visibility.Visible;
            }

            var bmp = LoadEntryPreviewBitmap(entry, _previewImageFileIndex);
            if (bmp != null)
            {
                EntryPreviewImage.Source = bmp;
                EntryPreviewImage.Visibility = Visibility.Visible;
            }

            if (_previewImageFiles is { Length: > 1 })
            {
                EntryPreviewImgIndex.Text = $"{_previewImageFileIndex + 1} / {_previewImageFiles.Length}";
                EntryPreviewImageNav.Visibility = Visibility.Visible;
            }

            if (entry.Type == EntryType.Image && entry.HasOcrPreviewBody)
            {
                EntryPreviewOcrSeparator.Visibility = Visibility.Visible;
                EntryPreviewOcrHeader.Visibility = Visibility.Visible;
                EntryPreviewOcrText.Text = entry.OcrPreviewBody;
                EntryPreviewOcrText.Visibility = Visibility.Visible;
            }

            if (bmp != null) return;

            SetEntryPreviewText(entry.Type == EntryType.Image
                ? "（无法解码该图片）"
                : string.Join(Environment.NewLine, entry.FilePaths ?? Array.Empty<string>()));
            EntryPreviewText.Visibility = Visibility.Visible;
            return;
        }

        SetEntryPreviewText(entry.FilePaths is { Length: > 0 }
            ? string.Join(Environment.NewLine, entry.FilePaths)
            : "（无路径）");
        EntryPreviewText.Visibility = Visibility.Visible;
    }

    private void UpdateEntryPreviewShortcutPhrase(ClipboardEntry entry)
    {
        if (string.IsNullOrWhiteSpace(entry.ShortcutPhrase)) return;

        var spec = SearchQuerySpec.Parse(_searchText.Trim());
        EntryPreviewShortcutPhraseChip.Content = new SearchMetadataChip(
            entry.ShortcutPhrase,
            !spec.IsEmpty && SourceMetadataPartMatchesQuery(entry.ShortcutPhrase, spec));
        EntryPreviewShortcutPhraseRow.Visibility = Visibility.Visible;
    }

    private void SetEntryPreviewText(string text)
    {
        var primary = TryFindResource("PrimaryText") as Brush ?? System.Windows.Media.Brushes.White;
        var accent = TryFindResource("AccentBg") as Brush ?? System.Windows.Media.Brushes.Teal;
        EntryPreviewText.Inlines.Clear();
        SearchHighlightInlines.Append(
            EntryPreviewText.Inlines,
            text,
            _searchText.Trim(),
            primary,
            accent,
            13,
            FontWeights.Normal);
    }

    private static void ClearEntryPreviewTextBlock(TextBlock textBlock)
    {
        textBlock.Text = "";
        textBlock.Inlines.Clear();
    }
}
