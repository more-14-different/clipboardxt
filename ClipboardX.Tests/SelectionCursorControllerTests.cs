using ClipboardManager;

namespace ClipboardX.Tests;

public sealed class SelectionCursorControllerTests
{
    [Fact]
    public void TogglePoint_FirstSelectedItemKeepsItSelected()
    {
        var controller = new SelectionCursorController();
        var selected = new HashSet<int> { 0 };

        var result = controller.TogglePoint(itemCount: 5, currentIndex: 0, selected);

        Assert.Equal([0], result.AddIndices);
        Assert.Empty(result.RemoveIndices);
        Assert.Equal(0, controller.PointCursor);
    }

    [Fact]
    public void TogglePoint_ActivePointCursorCanRemoveSelectedItem()
    {
        var controller = new SelectionCursorController();
        var selected = new HashSet<int> { 0 };
        var first = controller.TogglePoint(itemCount: 5, currentIndex: 0, selected);
        selected.UnionWith(first.AddIndices);

        var second = controller.TogglePoint(itemCount: 5, currentIndex: 0, selected);

        Assert.Empty(second.AddIndices);
        Assert.Equal([0], second.RemoveIndices);
    }

    [Fact]
    public void MovePoint_DoesNotMutateSelectedSet()
    {
        var controller = new SelectionCursorController();
        var selected = new HashSet<int> { 0, 2 };
        controller.TogglePoint(itemCount: 5, currentIndex: 0, selected);

        var cursor = controller.MovePoint(itemCount: 5, currentIndex: 0, delta: 1);

        Assert.Equal(1, cursor);
        Assert.Equal([0, 2], selected.Order());
    }

    [Fact]
    public void ExtendPoint_AddsRangeAndKeepsExistingSparseSelection()
    {
        var controller = new SelectionCursorController();
        var selected = new HashSet<int> { 0, 4 };
        controller.TogglePoint(itemCount: 6, currentIndex: 1, selected);

        var result = controller.ExtendPoint(itemCount: 6, currentIndex: 1, delta: 2);
        selected.UnionWith(result.AddIndices);

        Assert.Equal(3, controller.PointCursor);
        Assert.Equal([0, 1, 2, 3, 4], selected.Order());
    }

    [Fact]
    public void ExtendRange_ReplacesSelectionRange()
    {
        var controller = new SelectionCursorController();

        var result = controller.ExtendRange(itemCount: 5, currentIndex: 2, delta: 2);

        Assert.Equal([2, 3, 4], result.Indices);
        Assert.Equal(4, result.FocusIndex);
    }
}
