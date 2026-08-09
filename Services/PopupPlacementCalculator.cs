using System.Drawing;

namespace ClipboardManager;

internal static class PopupPlacementCalculator
{
    internal readonly record struct PhysicalSize(int Width, int Height);

    internal readonly record struct Position(int X, int Y);

    internal static PhysicalSize ToPhysicalSize(
        double width,
        double actualHeight,
        double maxHeight,
        double scaleX,
        double scaleY)
    {
        var height = actualHeight > 0 ? actualHeight : maxHeight;
        return new PhysicalSize(
            (int)(width * scaleX),
            (int)(height * scaleY));
    }

    internal static Position PlaceFixedAtTopLeft(
        Rectangle workArea,
        PhysicalSize popup,
        int margin = 16)
    {
        var x = workArea.Left + margin;
        var y = workArea.Top + margin;
        if (x + popup.Width > workArea.Right)
            x = Math.Max(workArea.Left, workArea.Right - popup.Width);
        if (y + popup.Height > workArea.Bottom)
            y = Math.Max(workArea.Top, workArea.Bottom - popup.Height);
        return new Position(x, y);
    }

    internal static Position PlaceNearAnchor(
        Rectangle workArea,
        PhysicalSize popup,
        double anchorX,
        double anchorY,
        int bottomFlipGap = 32)
    {
        var x = (int)anchorX;
        var y = (int)anchorY;
        if (x + popup.Width > workArea.Right)
            x = workArea.Right - popup.Width;
        if (y + popup.Height > workArea.Bottom)
            y = (int)anchorY - popup.Height - bottomFlipGap;
        if (x < workArea.Left)
            x = workArea.Left;
        if (y < workArea.Top)
            y = workArea.Top;
        return new Position(x, y);
    }
}
