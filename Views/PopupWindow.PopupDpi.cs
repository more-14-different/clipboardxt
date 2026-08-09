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
    /// <summary>
    /// 在 Show() 之前把即将使用的物理像素坐标写成 WPF Left/Top，避免窗口先在 (0,0) 露一帧再 SetWindowPos。
    /// 全局屏幕物理点不能仅用 CompositionTarget.TransformFromDevice：其随 HWND 所在监视器矩阵变化，跨 DPI 副屏会错（用户反馈「恢复成最初问题」）。
    /// 使用 MonitorFromRect（有 HWND 尺寸时）或 MonitorFromPoint + GetDpiForMonitor 计算相对监视器的 DIP 偏移；监视器原点用 PhysicalToLogical(桌面) 或 VirtualScreen 拼接（避免 API 恒等返回）。
    /// </summary>
    private bool TryApplyPendingPositionAsWpfLeftTop()
    {
        try
        {
            if (_hwnd == IntPtr.Zero)
            {
                var helper = new WindowInteropHelper(this);
                helper.EnsureHandle();
                _hwnd = helper.Handle;
            }

            int pw = 0, ph = 0;
            if (_hwnd != IntPtr.Zero && Win32.GetWindowRect(_hwnd, out var rcWin))
            {
                pw = rcWin.Right - rcWin.Left;
                ph = rcWin.Bottom - rcWin.Top;
            }

            if (PopupDpiMapper.TryPhysicalScreenTopLeftToWpfDip(
                    _pendingPhysX,
                    _pendingPhysY,
                    pw,
                    ph,
                    out var dipX,
                    out var dipY,
                    log: AgentDbgLog))
            {
                #region agent log
                AgentDbgLog("H1", "TryApplyPendingPositionAsWpfLeftTop", "per-monitor physical→DIP",
                    new { _pendingPhysX, _pendingPhysY, dipX, dipY });
                #endregion
                Left = dipX;
                Top = dipY;
                return true;
            }

            var src = HwndSource.FromHwnd(_hwnd);
            if (src?.CompositionTarget == null) return false;

            var dip = src.CompositionTarget.TransformFromDevice.Transform(
                new System.Windows.Point(_pendingPhysX, _pendingPhysY));
            #region agent log
            AgentDbgLog("H1", "TryApplyPendingPositionAsWpfLeftTop", "fallback TransformFromDevice",
                new { _pendingPhysX, _pendingPhysY, dip.X, dip.Y });
            #endregion
            Left = dip.X;
            Top = dip.Y;
            return true;
        }
        catch (Exception ex)
        {
            #region agent log
            AgentDbgLog("H1", "TryApplyPendingPositionAsWpfLeftTop", "exception",
                new { ex.GetType().Name, ex.Message });
            #endregion
            return false;
        }
    }

    /// <summary>
    /// DPI 切换或拖动结束后，用 HWND 物理矩形同步 WPF Left/Top（DIP）。
    /// 勿在跨屏拖动每一帧调用：全局物理坐标经 CompositionTarget.TransformFromDevice 时，若与 HWND 当前监视器 DPI 不一致会算错 Left/Top。
    /// </summary>
    private void SyncWindowPhysicalPositionToWpf(string syncSource)
    {
        if (_hwnd == IntPtr.Zero) return;
        if (!Win32.GetWindowRect(_hwnd, out var rc)) return;

        // 松手后第一次同步：壳可能在 WH_MOUSE_LL 与 Dispatcher 之间移动 HWND（H2 曾见 Left=-985）；拉回钩子最后一帧权威位置。
        if (_postDragHookAuthLeft != int.MinValue)
        {
            int authL = _postDragHookAuthLeft, authT = _postDragHookAuthTop;
            _postDragHookAuthLeft = int.MinValue;
            int dL = Math.Abs(rc.Left - authL);
            int dT = Math.Abs(rc.Top - authT);
            if (dL > 8 || dT > 8)
            {
                #region agent log
                AgentDbgLog("H15", "SyncWindowPhysicalPositionToWpf", "post-drag rect drift vs hook auth; restoring",
                    new { rc.Left, rc.Top, authL, authT, dL, dT });
                #endregion
                _isOurSetWindowPos = true;
                try
                {
                    Win32.SetWindowPos(_hwnd, IntPtr.Zero, authL, authT, 0, 0,
                        Win32.SWP_NOSIZE | Win32.SWP_NOZORDER | Win32.SWP_NOACTIVATE | Win32.SWP_NOSENDCHANGING);
                }
                finally
                {
                    _isOurSetWindowPos = false;
                }
                if (!Win32.GetWindowRect(_hwnd, out rc)) return;
            }
        }

        int vx = Win32.GetSystemMetrics(Win32.SM_XVIRTUALSCREEN);
        int vy = Win32.GetSystemMetrics(Win32.SM_YVIRTUALSCREEN);
        int vw = Win32.GetSystemMetrics(Win32.SM_CXVIRTUALSCREEN);
        int vh = Win32.GetSystemMetrics(Win32.SM_CYVIRTUALSCREEN);
        int w = rc.Right - rc.Left;
        int h = rc.Bottom - rc.Top;
        if (w > 0 && h > 0 && vw > 0 && vh > 0 &&
            (rc.Left < vx - 64 || rc.Top < vy - 64 || rc.Left > vx + vw - 32 || rc.Top > vy + vh - 32))
        {
            int ol = rc.Left, ot = rc.Top;
            int nl = Math.Clamp(rc.Left, vx, Math.Max(vx, vx + vw - w));
            int nt = Math.Clamp(rc.Top, vy, Math.Max(vy, vy + vh - h));
            _isOurSetWindowPos = true;
            try
            {
                Win32.SetWindowPos(_hwnd, IntPtr.Zero, nl, nt, 0, 0,
                    Win32.SWP_NOSIZE | Win32.SWP_NOZORDER | Win32.SWP_NOACTIVATE | Win32.SWP_NOSENDCHANGING);
            }
            finally
            {
                _isOurSetWindowPos = false;
            }
            if (!Win32.GetWindowRect(_hwnd, out rc)) return;
            #region agent log
            AgentDbgLog("H14", "SyncWindowPhysicalPositionToWpf", "clamped off-screen hwnd rect (virtual)",
                new { vx, vy, vw, vh, before = new { ol, ot }, after = new { nl, nt } });
            #endregion
        }

        _pendingPhysX = rc.Left;
        _pendingPhysY = rc.Top;
        #region agent log
        AgentDbgLog("H2", "SyncWindowPhysicalPositionToWpf", "before TryApply",
            new { syncSource, rc.Left, rc.Top, rc.Right, rc.Bottom, _pendingPhysX, _pendingPhysY });
        #endregion
        TryApplyPendingPositionAsWpfLeftTop();
    }
}

