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
using ClipboardManager.Services;

namespace ClipboardManager;

public partial class PopupWindow : Window
{
    private static BitmapSource? LoadEntryPreviewBitmap(ClipboardEntry entry, int imageFileIndex = 0)
    {
        try
        {
            if (entry.Type == EntryType.Image && entry.TryGetImageData() is { Length: > 0 } bytes)
                return ClipboardImageCodec.DecodePng(bytes, 520);

            var imagePaths = entry.GetImageFilePaths();
            if (imagePaths.Length > 0)
            {
                var p = imagePaths[Math.Clamp(imageFileIndex, 0, imagePaths.Length - 1)];
                if (!File.Exists(p)) return null;
                var bi = new BitmapImage();
                bi.BeginInit();
                bi.UriSource = new Uri(Path.GetFullPath(p));
                bi.CacheOption = BitmapCacheOption.OnLoad;
                bi.DecodePixelWidth = 520;
                bi.EndInit();
                bi.Freeze();
                return bi;
            }
        }
        catch
        {
            /* ignore */
        }

        return null;
    }

    private void NavigatePreviewImage(int delta)
    {
        if (!EntryPreviewPopup.IsOpen || _previewImageFiles is not { Length: > 1 }) return;
        _previewImageFileIndex = ((_previewImageFileIndex + delta) % _previewImageFiles.Length + _previewImageFiles.Length)
            % _previewImageFiles.Length;
        if (ItemsList.SelectedItem is ClipboardEntry entry)
            UpdateEntryPreviewBubbleContent(entry);
        PositionEntryPreviewPopup();
    }

    private void EntryPreviewImgPrev_Click(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        NavigatePreviewImage(-1);
    }

    private void EntryPreviewImgNext_Click(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        NavigatePreviewImage(1);
    }
}
