using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Brush = System.Windows.Media.Brush;
using Button = System.Windows.Controls.Button;
using Orientation = System.Windows.Controls.Orientation;
using TextBox = System.Windows.Controls.TextBox;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using ClipboardManager.Models;

namespace ClipboardManager;

public partial class PopupWindow : Window
{
    private void Settings_Click(object sender, MouseButtonEventArgs e)
    {
        HidePopup();
        SettingsRequested?.Invoke();
    }

    private void Header_DragStart(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left) return;
        e.Handled = true;
        _isDragging = true;
        _lastDragMoveTick = 0;
        _hasPendingDragMove = false;
        Win32.GetCursorPos(out _dragLastPt);
        Win32.GetWindowRect(_hwnd, out var rc0);
        _hookAuthPhysLeft = rc0.Left;
        _hookAuthPhysTop = rc0.Top;
        #region agent log
        _agentDbgDragMoveLogCount = 0;
        _agentDbgH20LogCount = 0;
        _agentDbgH21MismatchLogCount = 0;
        _agentDbgCachedPrimarySeamX = int.MinValue;
        try
        {
            var hPrim = Win32.MonitorFromPoint(new Win32.POINT { X = 0, Y = 0 }, Win32.MONITOR_DEFAULTTOPRIMARY);
            var miPrim = new Win32.MONITORINFO { cbSize = Marshal.SizeOf<Win32.MONITORINFO>() };
            if (hPrim != IntPtr.Zero && Win32.GetMonitorInfo(hPrim, ref miPrim))
                _agentDbgCachedPrimarySeamX = miPrim.rcMonitor.Right;
        }
        catch { /* 调试 */ }
        AgentDbgLog("H4", "Header_DragStart", "drag begin",
            new
            {
                _hwnd = _hwnd.ToInt64(),
                rc0.Left,
                rc0.Top,
                rc0.Right,
                rc0.Bottom,
                _dragLastPt.X,
                _dragLastPt.Y,
                wpfBeforeLeft = Left,
                wpfBeforeTop = Top,
                primarySeamX = _agentDbgCachedPrimarySeamX
            });
        #endregion
    }

}
