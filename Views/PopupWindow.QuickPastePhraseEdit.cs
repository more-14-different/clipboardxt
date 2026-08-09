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
    private ClipboardEntry? _phraseEditEntry;
    private readonly SearchEditorState _phraseEditor = new();

    private void AddQuickPaste(ClipboardEntry source)
    {
        _phraseEditEntry = source;
        var previewText = source.Preview;
        var preview = previewText.Length > 60
            ? previewText[..60] + "..."
            : previewText;
        PhrasePreview.Text = preview;
        var phrase = source.ShortcutPhrase ?? "";
        _phraseEditor.Restore(phrase, phrase.Length, -1);
        RefreshPhraseEditDisplay();
        PhraseEditPopup.IsOpen = true;
    }

    private void CommitPhraseEdit()
    {
        var phrase = _phraseEditor.Text.Trim();
        if (_phraseEditEntry == null)
        {
            ResetPhraseEditState();
            return;
        }

        var source = _phraseEditEntry;
        if (string.IsNullOrWhiteSpace(phrase))
        {
            if (source.IsQuickPaste)
            {
                _quickPastes.RemoveAll(q => q.Content == source.TextContent);
                _allItems.Remove(source);
                SaveQuickPastes();
            }
            else
            {
                source.ShortcutPhrase = null;
                if (source.PersistedId is long id)
                    _historyStore.TryUpdateShortcutPhrase(id, null);
            }

            RefreshFilter();
            ResetPhraseEditState();
            return;
        }

        if (source.IsQuickPaste)
        {
            _quickPastes.RemoveAll(q => q.Content == source.TextContent);
            _quickPastes.Add(new QuickPasteEntry { Phrase = phrase, Content = source.TextContent ?? "" });
            source.ShortcutPhrase = phrase;
        }
        else
        {
            source.ShortcutPhrase = phrase;
            if (source.IsArchived)
                _historyStore.TryRestoreArchived(source);
            if (source.PersistedId is long id)
                _historyStore.TryUpdateShortcutPhrase(id, phrase);
        }

        if (source.IsQuickPaste)
            SaveQuickPastes();
        RefreshFilter();

        ResetPhraseEditState();
    }

    private void ResetPhraseEditState()
    {
        EndPhraseMouseSelection();
        PhraseEditPopup.IsOpen = false;
        _phraseEditEntry = null;
        _phraseEditor.Reset();
    }

    private void SaveQuickPastes()
    {
        var settings = AppSettings.Load();
        settings.QuickPastes = _quickPastes;
        settings.Save();
    }

    private void PhraseConfirm_Click(object sender, MouseButtonEventArgs e) => CommitPhraseEdit();

    private void PhraseCancel_Click(object sender, MouseButtonEventArgs e)
    {
        ResetPhraseEditState();
    }
}
