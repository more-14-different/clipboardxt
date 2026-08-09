namespace ClipboardManager;

internal sealed class SearchEditorState
{
    private readonly SearchEditUndoHistory _undoHistory = new();

    public string Text { get; private set; } = "";
    public int CaretIndex { get; private set; }
    public int SelectionAnchor { get; private set; } = -1;
    public bool HasText => Text.Length > 0;

    public SearchEditUndoHistory.State Capture() =>
        new(Text, CaretIndex, SelectionAnchor);

    public void Reset()
    {
        Text = "";
        CaretIndex = 0;
        SelectionAnchor = -1;
        _undoHistory.Clear();
    }

    public void Restore(string? text, int caretIndex, int selectionAnchor)
    {
        var restoredText = text ?? "";
        Text = restoredText[..Math.Min(restoredText.Length, SearchEditorText.MaxLength)];
        CaretIndex = Math.Clamp(caretIndex, 0, Text.Length);
        SelectionAnchor = selectionAnchor < 0
            ? -1
            : Math.Clamp(selectionAnchor, 0, Text.Length);
        Clamp();
        _undoHistory.Clear();
    }

    public bool Clear()
    {
        if (Text.Length == 0) return false;
        RecordUndoState();
        Text = "";
        CaretIndex = 0;
        SelectionAnchor = -1;
        return true;
    }

    public void Insert(char character)
    {
        RecordUndoState();
        ReplaceSelectionIfAny();
        Text = Text.Insert(CaretIndex, character.ToString());
        CaretIndex++;
        SelectionAnchor = -1;
    }

    public bool InsertPastedText(string? text)
    {
        if (string.IsNullOrEmpty(text)) return false;
        var normalized = SearchEditorText.NormalizePastedText(text);
        if (normalized.Length == 0) return false;

        var selectedLength = TryGetSelection(out var selectionStart, out var selectionEnd)
            ? selectionEnd - selectionStart
            : 0;
        var remaining = SearchEditorText.MaxLength - (Text.Length - selectedLength);
        if (remaining <= 0) return false;

        RecordUndoState();
        ReplaceSelectionIfAny();
        if (normalized.Length > remaining)
            normalized = normalized[..remaining];

        Text = Text.Insert(CaretIndex, normalized);
        CaretIndex += normalized.Length;
        SelectionAnchor = -1;
        return true;
    }

    public void MoveCaretLeft(bool byUnit, bool extendSelection) =>
        MoveCaret(
            byUnit ? SearchEditorText.FindUnitLeft(Text, CaretIndex) : Math.Max(0, CaretIndex - 1),
            extendSelection);

    public void MoveCaretRight(bool byUnit, bool extendSelection) =>
        MoveCaret(
            byUnit ? SearchEditorText.FindUnitRight(Text, CaretIndex) : Math.Min(Text.Length, CaretIndex + 1),
            extendSelection);

    public void MoveCaret(int newIndex, bool extendSelection)
    {
        Clamp();
        newIndex = Math.Clamp(newIndex, 0, Text.Length);
        if (extendSelection)
        {
            if (SelectionAnchor < 0)
                SelectionAnchor = CaretIndex;
        }
        else
        {
            SelectionAnchor = -1;
        }

        CaretIndex = newIndex;
        Clamp();
    }

    public void SetSelection(int selectionAnchor, int caretIndex)
    {
        CaretIndex = Math.Clamp(caretIndex, 0, Text.Length);
        SelectionAnchor = selectionAnchor < 0
            ? -1
            : Math.Clamp(selectionAnchor, 0, Text.Length);
        Clamp();
    }

    public void SelectAll()
    {
        CaretIndex = Text.Length;
        SelectionAnchor = Text.Length > 0 ? 0 : -1;
    }

    public bool DeleteBackward(bool byUnit)
    {
        if (DeleteSelectionIfAny()) return true;
        if (CaretIndex <= 0) return false;
        var start = byUnit ? SearchEditorText.FindUnitLeft(Text, CaretIndex) : CaretIndex - 1;
        return DeleteRange(start, CaretIndex);
    }

    public bool DeleteForward(bool byUnit)
    {
        if (DeleteSelectionIfAny()) return true;
        if (CaretIndex >= Text.Length) return false;
        var end = byUnit ? SearchEditorText.FindUnitRight(Text, CaretIndex) : CaretIndex + 1;
        return DeleteRange(CaretIndex, end);
    }

    public bool DeleteRange(int start, int end)
    {
        start = Math.Clamp(start, 0, Text.Length);
        end = Math.Clamp(end, 0, Text.Length);
        if (end <= start) return false;

        RecordUndoState();
        Text = Text.Remove(start, end - start);
        CaretIndex = start;
        SelectionAnchor = -1;
        return true;
    }

    public bool TryGetSelection(out int start, out int end)
    {
        Clamp();
        if (SelectionAnchor >= 0 && SelectionAnchor != CaretIndex)
        {
            start = Math.Min(SelectionAnchor, CaretIndex);
            end = Math.Max(SelectionAnchor, CaretIndex);
            return true;
        }

        start = end = 0;
        return false;
    }

    public bool Undo()
    {
        if (!_undoHistory.TryUndo(Capture(), out var state)) return false;
        Restore(state);
        return true;
    }

    public bool Redo()
    {
        if (!_undoHistory.TryRedo(Capture(), out var state)) return false;
        Restore(state);
        return true;
    }

    private bool DeleteSelectionIfAny()
    {
        if (!TryGetSelection(out var start, out var end)) return false;
        return DeleteRange(start, end);
    }

    private void ReplaceSelectionIfAny()
    {
        if (!TryGetSelection(out var start, out var end)) return;
        Text = Text.Remove(start, end - start);
        CaretIndex = start;
        SelectionAnchor = -1;
    }

    private void RecordUndoState() =>
        _undoHistory.Push(Text, CaretIndex, SelectionAnchor);

    private void Restore(SearchEditUndoHistory.State state)
    {
        Text = state.Text;
        CaretIndex = state.CaretIndex;
        SelectionAnchor = state.SelectionAnchor;
        Clamp();
    }

    private void Clamp()
    {
        CaretIndex = Math.Clamp(CaretIndex, 0, Text.Length);
        if (SelectionAnchor > Text.Length)
            SelectionAnchor = Text.Length;
        if (SelectionAnchor == CaretIndex)
            SelectionAnchor = -1;
    }

}
