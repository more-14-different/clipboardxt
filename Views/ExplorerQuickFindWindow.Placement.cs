using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Brush = System.Windows.Media.Brush;

namespace ClipboardManager;

public partial class ExplorerQuickFindWindow : Window
{
    public void PositionNearExplorer(IntPtr explorerHwnd)
    {
        if (explorerHwnd == IntPtr.Zero || !Win32.IsWindow(explorerHwnd))
        {
            PositionNearCursor();
            return;
        }

        if (!Win32.GetWindowRect(explorerHwnd, out var rc))
        {
            PositionNearCursor();
            return;
        }

        var hMon = Win32.MonitorFromWindow(explorerHwnd, Win32.MONITOR_DEFAULTTONEAREST);
        var mi = new Win32.MONITORINFO { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<Win32.MONITORINFO>() };
        Win32.GetMonitorInfo(hMon, ref mi);

        var src = PresentationSource.FromVisual(this);
        double dipScale = 1;
        if (src?.CompositionTarget != null)
            dipScale = src.CompositionTarget.TransformFromDevice.M11;
        if (dipScale <= 0) dipScale = 1;

        double winW = ActualWidth > 0 ? ActualWidth : Width;
        double winH = ActualHeight > 0 ? ActualHeight : MaxHeight;

        double explorerRight = rc.Right * dipScale;
        double explorerLeft = rc.Left * dipScale;
        double explorerTop = rc.Top * dipScale;
        double explorerCenterY = (rc.Top + rc.Bottom) / 2.0 * dipScale;
        double screenRight = mi.rcWork.Right * dipScale;
        double screenLeft = mi.rcWork.Left * dipScale;

        double x;
        if (explorerRight + winW + 8 <= screenRight)
            x = explorerRight + 4;
        else if (explorerLeft - winW - 8 >= screenLeft)
            x = explorerLeft - winW - 4;
        else
            x = Math.Max(screenLeft, screenRight - winW - 16);

        double y = Math.Max(explorerTop, explorerCenterY - winH / 2);

        Left = x;
        Top = y;
    }

    private void PositionNearCursor()
    {
        Win32.GetCursorPos(out var pt);
        var src = PresentationSource.FromVisual(this);
        double dipScale = 1;
        if (src?.CompositionTarget != null)
            dipScale = src.CompositionTarget.TransformFromDevice.M11;
        if (dipScale <= 0) dipScale = 1;

        Left = pt.X * dipScale;
        Top = (pt.Y + 24) * dipScale;
    }
}
