using System.Windows;
using ClipboardManager;
using Point = System.Windows.Point;

namespace ClipboardX.Tests;

public sealed class SearchEditorMouseHitTestTests
{
    [Theory]
    [InlineData(-10, 0)]
    [InlineData(4.99, 0)]
    [InlineData(5, 1)]
    [InlineData(14.99, 1)]
    [InlineData(15, 2)]
    [InlineData(100, 3)]
    public void FindCaretIndex_UsesCharacterMidpoints(double x, int expected)
    {
        Assert.Equal(expected, SearchEditorMouseHitTest.FindCaretIndex([5, 15, 25], x));
    }

    [Fact]
    public void FindCaretIndex_EmptyLayoutReturnsZero()
    {
        Assert.Equal(0, SearchEditorMouseHitTest.FindCaretIndex([], 10));
    }

    [Theory]
    [InlineData(2, 5, 0)]
    [InlineData(18, 5, 2)]
    [InlineData(2, 25, 2)]
    [InlineData(18, 25, 4)]
    [InlineData(2, -10, 0)]
    [InlineData(18, 50, 4)]
    public void FindCaretIndex_WithCharacterBoundsSupportsWrappedRows(
        double x,
        double y,
        int expected)
    {
        Rect[] bounds =
        [
            new(0, 0, 10, 10),
            new(10, 0, 10, 10),
            new(0, 20, 10, 10),
            new(10, 20, 10, 10),
        ];

        Assert.Equal(expected, SearchEditorMouseHitTest.FindCaretIndex(bounds, new Point(x, y)));
    }

    [Theory]
    [InlineData(5, 5, 0)]
    [InlineData(15, 5, 1)]
    [InlineData(25, 5, -1)]
    [InlineData(5, 20, -1)]
    public void FindCharacterIndex_ReturnsContainingCharacter(double x, double y, int expected)
    {
        Rect[] bounds = [new(0, 0, 10, 10), new(10, 0, 10, 10)];

        Assert.Equal(expected, SearchEditorMouseHitTest.FindCharacterIndex(bounds, new Point(x, y)));
    }
}
