using System.Runtime.InteropServices;
using System.Windows;
using Brush = System.Windows.Media.Brush;

namespace ClipboardManager;

public partial class PopupWindow : Window
{
    private void RefreshPhraseEditDisplay()
    {
        var primary = TryFindResource("PrimaryText") as Brush ?? System.Windows.Media.Brushes.White;
        var muted = TryFindResource("MutedText") as Brush ?? System.Windows.Media.Brushes.Gray;
        var accent = TryFindResource("AccentBg") as Brush ?? System.Windows.Media.Brushes.Teal;
        PhraseMouseController.Render(primary, accent);
        if (!_phraseEditor.HasText)
        {
            PhraseEditDisplay.Foreground = muted;
            PhraseEditDisplay.Inlines.Add(new System.Windows.Documents.Run("在此输入…")
            {
                Foreground = muted,
                FontSize = 13
            });
        }
        else
        {
            PhraseEditDisplay.Foreground = primary;
        }
    }

    private void InsertPhraseEditChar(char ch)
    {
        if (_phraseEditor.Text.Length >= SearchEditorText.MaxLength
            && !_phraseEditor.TryGetSelection(out _, out _)) return;
        _phraseEditor.Insert(ch);
        RefreshPhraseEditDisplay();
    }

    private void MovePhraseEditCaretLeft(bool ctrl, bool shift) =>
        MovePhraseEditCaret(() => _phraseEditor.MoveCaretLeft(ctrl, shift));

    private void MovePhraseEditCaretRight(bool ctrl, bool shift) =>
        MovePhraseEditCaret(() => _phraseEditor.MoveCaretRight(ctrl, shift));

    private void MovePhraseEditCaret(int newIndex, bool extendSelection)
    {
        _phraseEditor.MoveCaret(newIndex, extendSelection);
        RefreshPhraseEditDisplay();
    }

    private void MovePhraseEditCaret(Action move)
    {
        move();
        RefreshPhraseEditDisplay();
    }

    private void DeletePhraseEditBackward(bool ctrl)
    {
        if (_phraseEditor.DeleteBackward(ctrl))
            RefreshPhraseEditDisplay();
    }

    private void DeletePhraseEditForward(bool ctrl)
    {
        if (_phraseEditor.DeleteForward(ctrl))
            RefreshPhraseEditDisplay();
    }

    private void SelectAllPhraseEditText()
    {
        _phraseEditor.SelectAll();
        RefreshPhraseEditDisplay();
    }

    private async Task PasteSystemClipboardIntoPhraseEditAsync()
    {
        for (var attempt = 0; attempt < 5 && _isPhraseEditPopupOpen; attempt++)
        {
            try
            {
                if (!System.Windows.Clipboard.ContainsText(System.Windows.TextDataFormat.UnicodeText)) return;
                if (_phraseEditor.InsertPastedText(
                        System.Windows.Clipboard.GetText(System.Windows.TextDataFormat.UnicodeText)))
                    RefreshPhraseEditDisplay();
                return;
            }
            catch (ExternalException)
            {
                if (attempt == 4) return;
                await Task.Delay(25);
            }
        }
    }

    private async Task CopyPhraseEditSelectionAsync()
    {
        if (!_phraseEditor.TryGetSelection(out var start, out var end)) return;
        await FastClipboardTextWriter.TrySetAsync(_hwnd, _phraseEditor.Text[start..end]);
    }

    private async Task CutPhraseEditSelectionAsync()
    {
        if (!_phraseEditor.TryGetSelection(out var start, out var end)) return;

        var expected = _phraseEditor.Capture();
        var selectedText = _phraseEditor.Text[start..end];
        if (!await FastClipboardTextWriter.TrySetAsync(_hwnd, selectedText)) return;
        if (_phraseEditor.Capture() != expected) return;

        if (_phraseEditor.DeleteRange(start, end))
            RefreshPhraseEditDisplay();
    }

    private void UndoPhraseEdit()
    {
        if (_phraseEditor.Undo())
            RefreshPhraseEditDisplay();
    }

    private void RedoPhraseEdit()
    {
        if (_phraseEditor.Redo())
            RefreshPhraseEditDisplay();
    }
}
