using ClipboardManager;

namespace ClipboardX.Tests;

public sealed class SearchEditorStateTests
{
    [Fact]
    public void Insert_ReplacesSelectionAndSupportsUndoRedo()
    {
        var state = StateWithText("alpha beta");
        state.SetSelection(6, 10);

        state.Insert('x');

        Assert.Equal("alpha x", state.Text);
        Assert.Equal(7, state.CaretIndex);
        Assert.Equal(-1, state.SelectionAnchor);
        Assert.True(state.Undo());
        Assert.Equal(new SearchEditUndoHistory.State("alpha beta", 10, 6), state.Capture());
        Assert.True(state.Redo());
        Assert.Equal(new SearchEditUndoHistory.State("alpha x", 7, -1), state.Capture());
    }

    [Fact]
    public void InsertPastedText_NormalizesAndHonorsMaximumAfterReplacingSelection()
    {
        var state = StateWithText(new string('a', SearchEditorText.MaxLength));
        state.SetSelection(SearchEditorText.MaxLength - 2, SearchEditorText.MaxLength);

        var changed = state.InsertPastedText("x\r\ny\tz");

        Assert.True(changed);
        Assert.EndsWith("x ", state.Text, StringComparison.Ordinal);
        Assert.Equal(SearchEditorText.MaxLength, state.Text.Length);
        Assert.Equal(SearchEditorText.MaxLength, state.CaretIndex);
    }

    [Fact]
    public void MoveCaretByUnit_UsesWordSymbolAndWhitespaceBoundaries()
    {
        var state = StateWithText("one  +two");
        state.MoveCaret(0, extendSelection: false);

        state.MoveCaretRight(byUnit: true, extendSelection: false);
        Assert.Equal(5, state.CaretIndex);
        state.MoveCaretRight(byUnit: true, extendSelection: false);
        Assert.Equal(6, state.CaretIndex);
        state.MoveCaretRight(byUnit: true, extendSelection: false);
        Assert.Equal(9, state.CaretIndex);
        state.MoveCaretLeft(byUnit: true, extendSelection: false);
        Assert.Equal(6, state.CaretIndex);
        state.MoveCaretLeft(byUnit: true, extendSelection: false);
        Assert.Equal(3, state.CaretIndex);
        state.MoveCaretLeft(byUnit: true, extendSelection: false);
        Assert.Equal(0, state.CaretIndex);
    }

    [Fact]
    public void MoveCaretWithShift_ExtendsAndThenClearsSelection()
    {
        var state = StateWithText("abcd");
        state.MoveCaret(2, extendSelection: false);

        state.MoveCaretRight(byUnit: false, extendSelection: true);

        Assert.True(state.TryGetSelection(out var start, out var end));
        Assert.Equal((2, 3), (start, end));
        state.MoveCaretLeft(byUnit: false, extendSelection: true);
        Assert.False(state.TryGetSelection(out _, out _));
        state.MoveCaret(4, extendSelection: false);
        Assert.Equal(-1, state.SelectionAnchor);
    }

    [Fact]
    public void DeleteBackwardAndForward_HandleUnitsAndSelections()
    {
        var state = StateWithText("one +two");

        Assert.True(state.DeleteBackward(byUnit: true));
        Assert.Equal("one +", state.Text);
        state.MoveCaret(0, extendSelection: false);
        Assert.True(state.DeleteForward(byUnit: true));
        Assert.Equal("+", state.Text);
        state.SetSelection(0, 1);
        Assert.True(state.DeleteBackward(byUnit: false));
        Assert.Equal("", state.Text);
        Assert.False(state.DeleteForward(byUnit: false));
    }

    [Fact]
    public void ClearCanUndoWhileResetClearsHistory()
    {
        var state = StateWithText("query");

        Assert.True(state.Clear());
        Assert.True(state.Undo());
        Assert.Equal("query", state.Text);

        state.Reset();

        Assert.Equal(new SearchEditUndoHistory.State("", 0, -1), state.Capture());
        Assert.False(state.Undo());
        Assert.False(state.Redo());
    }

    [Fact]
    public void SetSelection_ClampsIndicesAndNormalizesCollapsedSelection()
    {
        var state = StateWithText("abc");

        state.SetSelection(-10, 99);
        Assert.Equal(new SearchEditUndoHistory.State("abc", 3, -1), state.Capture());

        state.SetSelection(99, -10);
        Assert.Equal(new SearchEditUndoHistory.State("abc", 0, 3), state.Capture());
        Assert.True(state.TryGetSelection(out var start, out var end));
        Assert.Equal((0, 3), (start, end));
    }

    [Fact]
    public void Restore_RecoversTextCaretAndSelectionWithoutUndoHistory()
    {
        var state = StateWithText("old");

        state.Restore("remembered", 8, 2);

        Assert.Equal(new SearchEditUndoHistory.State("remembered", 8, 2), state.Capture());
        Assert.False(state.Undo());
        Assert.False(state.Redo());
    }

    private static SearchEditorState StateWithText(string text)
    {
        var state = new SearchEditorState();
        Assert.True(state.InsertPastedText(text));
        return state;
    }
}
