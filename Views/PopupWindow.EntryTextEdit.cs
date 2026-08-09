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
    private ClipboardEntry? _entryTextEditTarget;
    /// <summary>打开「编辑文本」前记录的原宿主 HWND，关闭编辑后用于恢复键盘焦点（宿主内光标位置由系统保留）。</summary>
    private IntPtr _textEditRestoreForegroundHwnd;
    /// <summary>编辑文本期间临时去掉 WS_EX_NOACTIVATE，关闭编辑后需写回。</summary>
    private bool _wsExNoActivateLiftedForEntryTextEdit;

    private void BeginEntryTextEdit(ClipboardEntry entry)
    {
        if (entry.Type != EntryType.Text) return;
        _textEditRestoreForegroundHwnd = IntPtr.Zero;
        if (_targetWindow != IntPtr.Zero && Win32.IsWindow(_targetWindow) && _targetWindow != _hwnd)
            _textEditRestoreForegroundHwnd = _targetWindow;

        CloseContextMenuPopup();
        _entryTextEditTarget = entry;
        EntryTextEditBox.Text = entry.TextContent ?? "";
        TextEntryEditPopup.IsOpen = true;
    }

    private void TextEntryEditPopup_Opened(object? sender, EventArgs e)
    {
        Dispatcher.BeginInvoke(() =>
        {
            LiftNoActivateForEntryTextEditIfNeeded();
            Win32.SetForegroundWindowAggressive(_hwnd);
            EntryTextEditBox.Focus();
            System.Windows.Input.Keyboard.Focus(EntryTextEditBox);
            EntryTextEditBox.SelectAll();
        }, DispatcherPriority.Input);
    }

    /// <summary>编辑弹窗紧贴 MainBorder 右侧，留出间隙避免被主面板遮挡。</summary>
    private CustomPopupPlacement[] TextEntryEditCustomPlacement(
        System.Windows.Size popupSize,
        System.Windows.Size targetSize,
        System.Windows.Point offset)
    {
        const double gap = 14;
        return new[]
        {
            new CustomPopupPlacement(new System.Windows.Point(targetSize.Width + gap, 0), PopupPrimaryAxis.Vertical)
        };
    }

    private void LiftNoActivateForEntryTextEditIfNeeded()
    {
        if (_hwnd == IntPtr.Zero) return;
        var ex = Win32.GetWindowLongPtr(_hwnd, Win32.GWL_EXSTYLE);
        var v = ex.ToInt64();
        if ((v & Win32.WS_EX_NOACTIVATE) == 0) return;
        _wsExNoActivateLiftedForEntryTextEdit = true;
        Win32.SetWindowLongPtr(_hwnd, Win32.GWL_EXSTYLE, new IntPtr(v & ~Win32.WS_EX_NOACTIVATE));
    }

    private void RestoreNoActivateAfterEntryTextEditIfLifted()
    {
        if (!_wsExNoActivateLiftedForEntryTextEdit) return;
        _wsExNoActivateLiftedForEntryTextEdit = false;
        if (_hwnd == IntPtr.Zero) return;
        var ex = Win32.GetWindowLongPtr(_hwnd, Win32.GWL_EXSTYLE);
        Win32.SetWindowLongPtr(_hwnd, Win32.GWL_EXSTYLE, new IntPtr(ex.ToInt64() | Win32.WS_EX_NOACTIVATE));
    }

    private void RestoreFocusAfterTextEntryEdit()
    {
        RestoreNoActivateAfterEntryTextEditIfLifted();
        var h = _textEditRestoreForegroundHwnd;
        _textEditRestoreForegroundHwnd = IntPtr.Zero;
        if (h == IntPtr.Zero || h == _hwnd || !Win32.IsWindow(h)) return;
        Win32.SetForegroundWindowAggressive(h);
    }

    private void CommitEntryTextEdit()
    {
        if (_entryTextEditTarget is not ClipboardEntry entry) return;
        var newText = EntryTextEditBox.Text ?? "";
        if (string.IsNullOrWhiteSpace(newText))
        {
            LocalizedMessageBox.Show("文本不能为空。", "编辑文本",
                MessageBoxButton.OK, MessageBoxImage.Information);
            Dispatcher.BeginInvoke(() =>
            {
                EntryTextEditBox.Focus();
                System.Windows.Input.Keyboard.Focus(EntryTextEditBox);
            }, DispatcherPriority.Input);
            return;
        }

        var oldText = entry.TextContent ?? "";
        if (entry.IsQuickPaste)
        {
            var phrase = entry.ShortcutPhrase ?? "";
            for (var i = 0; i < _quickPastes.Count; i++)
            {
                if (_quickPastes[i].Content != oldText) continue;
                if (!string.IsNullOrEmpty(phrase) && _quickPastes[i].Phrase != phrase) continue;
                var ph = _quickPastes[i].Phrase;
                _quickPastes[i] = new QuickPasteEntry { Phrase = ph, Content = newText };
                break;
            }
            entry.TextContent = newText;
            SaveQuickPastes();
        }
        else
        {
            entry.TextContent = newText;
            if (entry.IsArchived)
                _historyStore.TryRestoreArchived(entry);
            if (entry.PersistedId is long pid)
                _historyStore.TryUpdateText(pid, newText);
        }

        entry.RaiseTextDisplayPropertiesChanged();
        RefreshFilter();
        TextEntryEditPopup.IsOpen = false;
        _entryTextEditTarget = null;
        Dispatcher.BeginInvoke(RestoreFocusAfterTextEntryEdit, DispatcherPriority.Background);
    }

    private void CancelEntryTextEdit()
    {
        TextEntryEditPopup.IsOpen = false;
        _entryTextEditTarget = null;
        Dispatcher.BeginInvoke(RestoreFocusAfterTextEntryEdit, DispatcherPriority.Background);
    }

    private void CtxEditText_Click(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        ActivateCtxEditText();
    }

    private void ActivateCtxEditText()
    {
        CloseContextMenuPopup();
        if (_contextEntry is { Type: EntryType.Text })
            BeginEntryTextEdit(_contextEntry);
    }

    private void EntryTextEditSave_Click(object sender, MouseButtonEventArgs e) => CommitEntryTextEdit();

    private void EntryTextEditCancel_Click(object sender, MouseButtonEventArgs e) => CancelEntryTextEdit();

    private void EntryTextEditBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Escape)
        {
            e.Handled = true;
            CancelEntryTextEdit();
            return;
        }

        if (e.Key == System.Windows.Input.Key.Enter
            && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            e.Handled = true;
            CommitEntryTextEdit();
        }
    }

}
