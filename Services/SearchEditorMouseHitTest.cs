using System.Windows;
using Point = System.Windows.Point;

namespace ClipboardManager;

internal static class SearchEditorMouseHitTest
{
    public static int FindCaretIndex(IReadOnlyList<double> caretMidpoints, double x)
    {
        for (var i = 0; i < caretMidpoints.Count; i++)
        {
            if (x < caretMidpoints[i]) return i;
        }

        return caretMidpoints.Count;
    }

    public static int FindCaretIndex(IReadOnlyList<Rect> characterBounds, Point point)
    {
        if (characterBounds.Count == 0) return 0;

        var rowTop = double.NaN;
        var rowBottom = double.NaN;
        var nearestRowDistance = double.MaxValue;
        for (var i = 0; i < characterBounds.Count; i++)
        {
            var bounds = characterBounds[i];
            var distance = point.Y < bounds.Top
                ? bounds.Top - point.Y
                : point.Y > bounds.Bottom
                    ? point.Y - bounds.Bottom
                    : 0;
            if (distance >= nearestRowDistance) continue;
            nearestRowDistance = distance;
            rowTop = bounds.Top;
            rowBottom = bounds.Bottom;
            if (distance == 0) break;
        }

        var firstIndex = -1;
        var lastIndex = -1;
        for (var i = 0; i < characterBounds.Count; i++)
        {
            var bounds = characterBounds[i];
            if (bounds.Bottom < rowTop || bounds.Top > rowBottom) continue;
            if (firstIndex < 0) firstIndex = i;
            lastIndex = i;
            if (point.X < bounds.Left + bounds.Width / 2)
                return i;
        }

        return lastIndex >= 0 ? lastIndex + 1 : Math.Max(0, firstIndex);
    }

    public static int FindCharacterIndex(IReadOnlyList<Rect> characterBounds, Point point)
    {
        for (var i = 0; i < characterBounds.Count; i++)
        {
            if (characterBounds[i].Contains(point)) return i;
        }

        return -1;
    }
}
