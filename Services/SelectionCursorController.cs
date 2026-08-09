namespace ClipboardManager;

internal sealed class SelectionCursorController
{
    public int RangeAnchor { get; private set; } = -1;
    public int RangeCursor { get; private set; } = -1;
    public int MouseAnchor { get; private set; } = -1;
    public int PointCursor { get; private set; } = -1;

    public bool HasPointCursor => PointCursor >= 0;

    public void Reset()
    {
        RangeAnchor = -1;
        RangeCursor = -1;
        MouseAnchor = -1;
        PointCursor = -1;
    }

    public void ClearPointCursor()
    {
        PointCursor = -1;
    }

    public int MoveSingle(int itemCount, int currentIndex, int delta)
    {
        ClearRangeAndPoint();
        var next = ClampIndex(itemCount, currentIndex + delta);
        MouseAnchor = next;
        return next;
    }

    public int MoveSingleTo(int itemCount, int index)
    {
        ClearRangeAndPoint();
        var next = ClampIndex(itemCount, index);
        MouseAnchor = next;
        return next;
    }

    public RangeSelectionResult ExtendRange(int itemCount, int currentIndex, int delta)
    {
        ClearPointCursor();
        if (itemCount <= 0)
            return RangeSelectionResult.Empty;

        if (RangeAnchor < 0)
        {
            var cur = ClampIndex(itemCount, currentIndex >= 0 ? currentIndex : 0);
            RangeAnchor = cur;
            RangeCursor = cur;
        }

        RangeCursor = ClampIndex(itemCount, RangeCursor + delta);
        MouseAnchor = RangeCursor;
        return RangeSelectionResult.FromRange(RangeAnchor, RangeCursor);
    }

    public void SetMouseAnchor(int itemCount, int index)
    {
        var next = ClampIndex(itemCount, index);
        MouseAnchor = next;
        RangeAnchor = next;
        RangeCursor = next;
    }

    public RangeSelectionResult SelectMouseRange(int itemCount, int index, int currentIndex)
    {
        if (itemCount <= 0)
            return RangeSelectionResult.Empty;

        var idx = ClampIndex(itemCount, index);
        var anchor = MouseAnchor >= 0 ? MouseAnchor : ClampIndex(itemCount, currentIndex >= 0 ? currentIndex : idx);
        RangeAnchor = Math.Min(anchor, idx);
        RangeCursor = Math.Max(anchor, idx);
        SetPointCursor(itemCount, idx);
        return RangeSelectionResult.FromRange(anchor, idx);
    }

    public PointSelectionResult TogglePoint(int itemCount, int currentIndex, IReadOnlySet<int> selectedIndices)
    {
        if (itemCount <= 0)
            return PointSelectionResult.None;

        var pointModeWasActive = HasPointCursor;
        var idx = GetPointCursorOrSelectedIndex(itemCount, currentIndex);
        bool selected = selectedIndices.Contains(idx);
        bool remove = selected && (pointModeWasActive || selectedIndices.Count > 1);
        SetPointCursor(itemCount, idx);
        return remove
            ? PointSelectionResult.Remove(idx, idx)
            : PointSelectionResult.Add(idx, idx);
    }

    public PointSelectionResult ExtendPoint(int itemCount, int currentIndex, int delta)
    {
        if (itemCount <= 0)
            return PointSelectionResult.None;

        var anchor = GetPointCursorOrSelectedIndex(itemCount, currentIndex);
        var end = ClampIndex(itemCount, anchor + delta);
        SetPointCursor(itemCount, end);
        return PointSelectionResult.AddRange(anchor, end, end);
    }

    public int MovePoint(int itemCount, int currentIndex, int delta)
    {
        var next = ClampIndex(itemCount, GetPointCursorOrSelectedIndex(itemCount, currentIndex) + delta);
        SetPointCursor(itemCount, next);
        return next;
    }

    public int MovePointTo(int itemCount, int index)
    {
        var next = ClampIndex(itemCount, index);
        SetPointCursor(itemCount, next);
        return next;
    }

    private int GetPointCursorOrSelectedIndex(int itemCount, int currentIndex) =>
        ClampIndex(itemCount, PointCursor >= 0 ? PointCursor : (currentIndex >= 0 ? currentIndex : 0));

    public void SetPointCursor(int itemCount, int index)
    {
        PointCursor = ClampIndex(itemCount, index);
        MouseAnchor = PointCursor;
        RangeAnchor = PointCursor;
        RangeCursor = PointCursor;
    }

    private void ClearRangeAndPoint()
    {
        RangeAnchor = -1;
        RangeCursor = -1;
        PointCursor = -1;
    }

    private static int ClampIndex(int itemCount, int index)
    {
        if (itemCount <= 0) return -1;
        return Math.Clamp(index, 0, itemCount - 1);
    }
}

internal sealed record RangeSelectionResult(IReadOnlyList<int> Indices, int FocusIndex)
{
    public static RangeSelectionResult Empty { get; } = new([], -1);

    public static RangeSelectionResult FromRange(int a, int b)
    {
        var start = Math.Min(a, b);
        var end = Math.Max(a, b);
        var indices = Enumerable.Range(start, end - start + 1).ToArray();
        return new RangeSelectionResult(indices, b);
    }
}

internal sealed record PointSelectionResult(
    IReadOnlyList<int> AddIndices,
    IReadOnlyList<int> RemoveIndices,
    int PointCursorIndex)
{
    public static PointSelectionResult None { get; } = new([], [], -1);

    public static PointSelectionResult Add(int index, int cursorIndex) =>
        new([index], [], cursorIndex);

    public static PointSelectionResult Remove(int index, int cursorIndex) =>
        new([], [index], cursorIndex);

    public static PointSelectionResult AddRange(int a, int b, int cursorIndex)
    {
        var start = Math.Min(a, b);
        var end = Math.Max(a, b);
        return new PointSelectionResult(Enumerable.Range(start, end - start + 1).ToArray(), [], cursorIndex);
    }
}
