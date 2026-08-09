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
    private enum ClipboardItemAction
    {
        Paste,
        PasteAsFile,
        PasteJson,
        EditText,
        ShortcutPhrase,
        ToggleStar,
        Delete
    }

    /// <summary>
    /// 由 WH_KEYBOARD_LL 回调线程调用。这里只允许读取普通配置字段和键盘状态；
    /// WPF Popup、ItemsList 及选中项必须等 Dispatcher 切回 UI 线程后再访问。
    /// </summary>
    private bool TryDispatchClipboardItemActionHotkey(uint vkCode)
    {
        if (_appSettings == null) return false;

        bool Match(uint modifiers, uint key) => key == vkCode && HotkeyChordMatches(modifiers);
        bool Dispatch(ClipboardItemAction action)
        {
            Dispatcher.BeginInvoke(() => ActivateClipboardItemActionFromHotkey(action));
            return true;
        }

        if (Match(_appSettings.ClipboardPasteHotkeyModifiers, _appSettings.ClipboardPasteHotkeyKey))
            return Dispatch(ClipboardItemAction.Paste);
        if (Match(_appSettings.ClipboardPasteAsFileHotkeyModifiers, _appSettings.ClipboardPasteAsFileHotkeyKey))
            return Dispatch(ClipboardItemAction.PasteAsFile);
        if (Match(_appSettings.ClipboardPasteJsonHotkeyModifiers, _appSettings.ClipboardPasteJsonHotkeyKey))
            return Dispatch(ClipboardItemAction.PasteJson);
        if (Match(_appSettings.ClipboardEditTextHotkeyModifiers, _appSettings.ClipboardEditTextHotkeyKey))
            return Dispatch(ClipboardItemAction.EditText);
        if (Match(_appSettings.ClipboardShortcutPhraseHotkeyModifiers, _appSettings.ClipboardShortcutPhraseHotkeyKey))
            return Dispatch(ClipboardItemAction.ShortcutPhrase);
        if (Match(_appSettings.StarToggleHotkeyModifiers, _appSettings.StarToggleHotkeyKey))
            return Dispatch(ClipboardItemAction.ToggleStar);
        if (Match(_appSettings.ClipboardDeleteHotkeyModifiers, _appSettings.ClipboardDeleteHotkeyKey))
            return Dispatch(ClipboardItemAction.Delete);
        return false;
    }

    private void ActivateClipboardItemActionFromHotkey(ClipboardItemAction action)
    {
        Dispatcher.VerifyAccess();
        var contextOpen = ContextPopup.IsOpen;
        var entry = contextOpen ? _contextEntry : ItemsList.SelectedItem as ClipboardEntry;
        if (entry == null) return;
        _contextEntry = entry;

        switch (action)
        {
            case ClipboardItemAction.Paste:
                if (contextOpen) ActivateCtxPaste();
                else HandleMainEnterKey();
                break;
            case ClipboardItemAction.PasteAsFile when entry.Type is EntryType.Image or EntryType.Text:
                ActivateCtxPasteAsFile();
                break;
            case ClipboardItemAction.PasteJson
                when entry.Type == EntryType.Text && IsWellFormedJson(entry.TextContent):
                ActivateCtxPasteJsonFile();
                break;
            case ClipboardItemAction.EditText when entry.Type == EntryType.Text:
                ActivateCtxEditText();
                break;
            case ClipboardItemAction.ShortcutPhrase:
                ActivateCtxShortcut();
                break;
            case ClipboardItemAction.ToggleStar when !entry.IsQuickPaste:
                if (contextOpen) ActivateCtxStar();
                else ToggleStarForCurrentSelection();
                break;
            case ClipboardItemAction.Delete:
                // 无修饰 Delete 在搜索输入中仍用于删字符；清空搜索后才是条目动作。
                if (!contextOpen && _searchText.Length > 0 && _appSettings?.ClipboardDeleteHotkeyModifiers == 0)
                {
                    DeleteSearchForward(ctrl: false);
                    break;
                }
                if (contextOpen) ActivateCtxDelete();
                else DeleteSelectedItemWithConfirm();
                break;
        }
    }

    private void ActivateCtxPaste()
    {
        CloseContextMenuPopup();
        if (_contextEntry != null)
        {
            ItemsList.SelectedItem = _contextEntry;
            PasteSelectedItem();
        }
    }

    private void ActivateCtxPasteAsFile()
    {
        CloseContextMenuPopup();
        if (_contextEntry is { } entry && entry.Type is EntryType.Image or EntryType.Text)
        {
            ItemsList.SelectedItem = entry;
            if (entry.Type == EntryType.Image)
                PasteImageAsFileForExplorer();
            else if (IsWellFormedJson(entry.TextContent))
                PasteJsonAsFileForExplorer();
            else
                PasteTextAsFileForExplorer();
        }
    }

    private void ActivateCtxPasteJsonFile()
    {
        CloseContextMenuPopup();
        if (_contextEntry is { Type: EntryType.Text } && IsWellFormedJson(_contextEntry.TextContent))
        {
            ItemsList.SelectedItem = _contextEntry;
            PasteJsonAsFileForExplorer();
        }
    }

    private void ActivateCtxShortcut()
    {
        CloseContextMenuPopup();
        if (_contextEntry != null)
            AddQuickPaste(_contextEntry);
    }

    private void ActivateCtxStar()
    {
        CloseContextMenuPopup();
        if (_contextEntry == null) return;
        ItemsList.SelectedItem = _contextEntry;
        ToggleStarForEntries([_contextEntry]);
    }

    private void ActivateCtxDelete()
    {
        var del = _contextEntry;
        CloseContextMenuPopup();
        if (del != null)
            RemoveEntry(del);
    }
}
