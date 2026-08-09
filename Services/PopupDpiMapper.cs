using System.Windows;

namespace ClipboardManager;

internal static class PopupDpiMapper
{
    public static bool TryPhysicalScreenTopLeftToWpfDip(
        int physX,
        int physY,
        int physW,
        int physH,
        out double dipX,
        out double dipY,
        bool logDiagnostics = true,
        Action<string, string, string, object?>? log = null)
    {
        dipX = dipY = 0;
        IntPtr hMon;
        string monitorPick;
        if (physW > 0 && physH > 0)
        {
            var rcSel = new Win32.RECT
            {
                Left = physX,
                Top = physY,
                Right = physX + physW,
                Bottom = physY + physH
            };
            hMon = Win32.MonitorFromRect(ref rcSel, Win32.MONITOR_DEFAULTTONEAREST);
            monitorPick = "rect";
        }
        else
        {
            hMon = IntPtr.Zero;
            monitorPick = "point";
        }

        if (hMon == IntPtr.Zero)
        {
            var pt = new Win32.POINT { X = physX, Y = physY };
            hMon = Win32.MonitorFromPoint(pt, Win32.MONITOR_DEFAULTTONEAREST);
            monitorPick = "point";
        }

        if (hMon == IntPtr.Zero) return false;

        var mi = new Win32.MONITORINFO { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<Win32.MONITORINFO>() };
        if (!Win32.GetMonitorInfo(hMon, ref mi)) return false;

        uint dpiX = 96, dpiY = 96;
        if (Win32.GetDpiForMonitor(hMon, 0, out uint dpx, out uint dpy) == 0)
        {
            dpiX = dpx;
            dpiY = dpy;
        }

        double relPhysX = physX - mi.rcMonitor.Left;
        double relPhysY = physY - mi.rcMonitor.Top;
        double relLogX = relPhysX * 96.0 / dpiX;
        double relLogY = relPhysY * 96.0 / dpiY;

        var desk = Win32.GetDesktopWindow();
        if (desk == IntPtr.Zero) return false;

        var monPt = new Win32.POINT { X = mi.rcMonitor.Left, Y = mi.rcMonitor.Top };
        if (!Win32.ScreenToClient(desk, ref monPt))
            return false;

        var monLog = monPt;
        bool physToLogOk = Win32.PhysicalToLogicalPointForPerMonitorDPI(desk, ref monLog);

        // PhysicalToLogical can still return physical-like values for monitor origins.
        bool identitySuspect = physToLogOk &&
            Math.Abs(monLog.X - mi.rcMonitor.Left) < 1.0 &&
            Math.Abs(monLog.Y - mi.rcMonitor.Top) < 1.0 &&
            (dpiX > 96 || dpiY > 96);

        double monLeftDip, monTopDip;
        if (!physToLogOk || identitySuspect)
        {
            int vx = Win32.GetSystemMetrics(Win32.SM_XVIRTUALSCREEN);
            int vy = Win32.GetSystemMetrics(Win32.SM_YVIRTUALSCREEN);
            monLeftDip = SystemParameters.VirtualScreenLeft + (mi.rcMonitor.Left - vx) * 96.0 / dpiX;
            monTopDip = SystemParameters.VirtualScreenTop + (mi.rcMonitor.Top - vy) * 96.0 / dpiY;
        }
        else
        {
            monLeftDip = monLog.X;
            monTopDip = monLog.Y;
        }

        dipX = monLeftDip + relLogX;
        dipY = monTopDip + relLogY;

        if (logDiagnostics && log != null)
        {
            int vxLog = Win32.GetSystemMetrics(Win32.SM_XVIRTUALSCREEN);
            int vyLog = Win32.GetSystemMetrics(Win32.SM_YVIRTUALSCREEN);
            log("H19", "TryPhysicalScreenTopLeftToWpfDip", "origin+dip",
                new
                {
                    monitorPick,
                    physW,
                    physH,
                    physToLogOk,
                    identitySuspect,
                    branch = !physToLogOk || identitySuspect ? "virtual" : "ptol",
                    monPtClient = new { monPt.X, monPt.Y },
                    monLog = new { monLog.X, monLog.Y },
                    rcMon = new { mi.rcMonitor.Left, mi.rcMonitor.Top },
                    vxLog,
                    vyLog,
                    dpiX,
                    dpiY,
                    monLeftDip,
                    monTopDip,
                    dipX,
                    dipY
                });
        }

        return true;
    }
}
