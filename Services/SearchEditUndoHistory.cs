namespace ClipboardManager;

internal sealed class SearchEditUndoHistory
{
    internal readonly record struct State(string Text, int CaretIndex, int SelectionAnchor);

    private const int MaxStates = 100;
    private readonly List<State> _undoStates = new();
    private readonly List<State> _redoStates = new();

    public int UndoCount => _undoStates.Count;
    public int RedoCount => _redoStates.Count;

    public void Clear()
    {
        _undoStates.Clear();
        _redoStates.Clear();
    }

    public void Push(string text, int caretIndex, int selectionAnchor)
    {
        var state = new State(text, caretIndex, selectionAnchor);
        if (_undoStates.Count > 0 && _undoStates[^1] == state) return;
        PushCapped(_undoStates, state);
        _redoStates.Clear();
    }

    public bool TryUndo(State current, out State state)
    {
        if (!TryPop(_undoStates, out state))
        {
            return false;
        }

        PushCapped(_redoStates, current);
        return true;
    }

    public bool TryRedo(State current, out State state)
    {
        if (!TryPop(_redoStates, out state))
            return false;

        PushCapped(_undoStates, current);
        return true;
    }

    private static void PushCapped(List<State> states, State state)
    {
        if (states.Count >= MaxStates)
            states.RemoveAt(0);
        states.Add(state);
    }

    private static bool TryPop(List<State> states, out State state)
    {
        if (states.Count == 0)
        {
            state = default;
            return false;
        }

        var index = states.Count - 1;
        state = states[index];
        states.RemoveAt(index);
        return true;
    }
}
