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
    private void SyncContextMenuForEntry(ClipboardEntry entry)
    {
        _contextEntry = entry;
        if (!ItemsList.SelectedItems.Contains(entry))
        {
            ItemsList.SelectedItems.Clear();
            ItemsList.SelectedItems.Add(entry);
        }
        CtxStarText.Text = entry.IsStarred ? "☆ 取消收藏" : "★ 收藏";
        CtxStarBorder.Visibility = entry.IsQuickPaste ? Visibility.Collapsed : Visibility.Visible;
        CtxShortcutText.Text = !string.IsNullOrWhiteSpace(entry.ShortcutPhrase) ? "⚡ 修改快捷短语" : "⚡ 设为快捷短语";
        CtxPasteAsFileBorder.Visibility = ItemsList.SelectedItems.Cast<ClipboardEntry>()
            .Any(ClipboardFileExportPlanner.CanExport)
            ? Visibility.Visible
            : Visibility.Collapsed;
        CtxPasteJsonFileBorder.Visibility = entry.Type == EntryType.Text && IsWellFormedJson(entry.TextContent)
            ? Visibility.Visible
            : Visibility.Collapsed;
        CtxEditTextBorder.Visibility = entry.Type == EntryType.Text
            ? Visibility.Visible
            : Visibility.Collapsed;
        if (_appSettings != null)
        {
            CtxPasteHotkeyText.Text = _appSettings.ClipboardPasteHotkeyDisplayName;
            CtxPasteAsFileHotkeyText.Text = _appSettings.ClipboardPasteAsFileHotkeyDisplayName;
            CtxPasteJsonHotkeyText.Text = _appSettings.ClipboardPasteJsonHotkeyDisplayName;
            CtxEditTextHotkeyText.Text = _appSettings.ClipboardEditTextHotkeyDisplayName;
            CtxShortcutHotkeyText.Text = _appSettings.ClipboardShortcutPhraseHotkeyDisplayName;
            CtxStarHotkeyText.Text = _appSettings.StarToggleHotkeyDisplayName;
            CtxDeleteHotkeyText.Text = _appSettings.ClipboardDeleteHotkeyDisplayName + " ×2";
        }
    }
}
