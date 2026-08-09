using ClipboardManager;

namespace ClipboardX.Tests;

public sealed class SearchEditUndoHistoryTests
{
    [Fact]
    public void UndoAndRedo_MoveCurrentStateBetweenStacks()
    {
        var history = new SearchEditUndoHistory();
        history.Push("a", 1, -1);
        history.Push("ab", 2, 0);

        Assert.True(history.TryUndo(new("abc", 3, -1), out var second));
        Assert.Equal(new SearchEditUndoHistory.State("ab", 2, 0), second);
        Assert.True(history.TryUndo(second, out var first));
        Assert.Equal(new SearchEditUndoHistory.State("a", 1, -1), first);
        Assert.True(history.TryRedo(first, out var redone));
        Assert.Equal(second, redone);
        Assert.True(history.TryRedo(redone, out var latest));
        Assert.Equal(new SearchEditUndoHistory.State("abc", 3, -1), latest);
        Assert.False(history.TryRedo(latest, out _));
    }

    [Fact]
    public void Push_DeduplicatesAdjacentStates()
    {
        var history = new SearchEditUndoHistory();
        history.Push("query", 5, -1);
        history.Push("query", 5, -1);

        Assert.Equal(1, history.UndoCount);
    }

    [Fact]
    public void Push_CapsHistoryAtOneHundredStates()
    {
        var history = new SearchEditUndoHistory();
        for (var i = 0; i < 120; i++)
            history.Push(i.ToString(), i, -1);

        Assert.Equal(100, history.UndoCount);
        var current = new SearchEditUndoHistory.State("120", 3, -1);
        for (var i = 119; i >= 20; i--)
        {
            Assert.True(history.TryUndo(current, out var state));
            Assert.Equal(i.ToString(), state.Text);
            current = state;
        }
        Assert.False(history.TryUndo(current, out _));
    }

    [Fact]
    public void PushAfterUndo_ClearsRedoBranch()
    {
        var history = new SearchEditUndoHistory();
        history.Push("", 0, -1);
        history.Push("a", 1, -1);
        Assert.True(history.TryUndo(new("ab", 2, -1), out var undone));

        history.Push(undone.Text, undone.CaretIndex, undone.SelectionAnchor);

        Assert.Equal(0, history.RedoCount);
        Assert.False(history.TryRedo(new("ac", 2, -1), out _));
    }
}
