using System;
using System.Diagnostics;
using System.Windows;

namespace ClipboardManager;

public partial class FileDialogJumpPickerWindow : Window
{
    private void DockFollowTick(bool force = false)
    {
        var sw = Stopwatch.StartNew();
        if (!_dockBesideDialog || _hwnd == IntPtr.Zero) return;
        if (!TryReadDockOwnerRect(out var ownerRect))
        {
            // 如果窗口句柄已失效，直接关闭
            if (_fileDialogOwnerHwnd == IntPtr.Zero || !Win32.IsWindow(_fileDialogOwnerHwnd))
            {
                ShellNavigateLog.Write("filejump", "Picker Closed: DockFollowTick (Owner window destroyed)");
                Close();
                return;
            }

            // 如果窗口还在但不可见（可能宿主是 Electron/VSCode，对话框还在初始化或被隐藏）
            // 给予 2 秒的宽限期，避免对话框显示太慢导致面板直接被关掉。
            if (Environment.TickCount64 - _loadedTick < 2000)
            {
                return; // 暂不关闭，等待它可见
            }

            ShellNavigateLog.Write("filejump", "Picker Closed: DockFollowTick (Owner window invisible after grace period)");
            Close();
            return;
        }

        UpdateDockPopupPhysicalSizeCache();
        var actualWidth = _dockPopupPhysWidth;
        var actualHeight = _dockPopupPhysHeight;
        if (!force
            && ownerRect.Left == _lastDockOwnerLeft
            && ownerRect.Top == _lastDockOwnerTop
            && ownerRect.Right == _lastDockOwnerRight
            && ownerRect.Bottom == _lastDockOwnerBottom
            && actualWidth == _lastDockActualWidth
            && actualHeight == _lastDockActualHeight)
            return;

        try
        {
            TryRealtimeDockFollow(force);
            RememberDockSnapshot(ownerRect, actualWidth, actualHeight);
        }
        catch { /* ignore */ }
        finally
        {
            sw.Stop();
            if (sw.ElapsedMilliseconds >= 8 && _perfDockFollowSlowLogCount < 60)
            {
                _perfDockFollowSlowLogCount++;
                ClipboardDiagnosticsLog.Write(
                    $"filejump.perf dock_follow_tick elapsedMs={sw.ElapsedMilliseconds} force={force}");
            }
        }
    }

    private void TryRealtimeDockFollow(bool force = false)
    {
        var sw = Stopwatch.StartNew();
        var hwnd = _hwnd;
        if (!_dockBesideDialog || hwnd == IntPtr.Zero || !Win32.IsWindow(hwnd)) return;
        if (!TryReadDockOwnerRect(out var ownerRect)) return;

        var popupW = _dockPopupPhysWidth;
        var popupH = _dockPopupPhysHeight;
        if (popupW <= 0 || popupH <= 0) return;
        if (!FileJumpPickerDockPlacement.TryComputePosition(ownerRect, popupW, popupH, out var x, out var y))
            return;

        if (!force && x == _lastAppliedPhysX && y == _lastAppliedPhysY)
            return;

        _isOurSetWindowPosForPicker = true;
        try
        {
            Win32.SetWindowPos(hwnd, IntPtr.Zero, x, y, 0, 0,
                Win32.SWP_NOSIZE | Win32.SWP_NOZORDER | Win32.SWP_NOACTIVATE | Win32.SWP_NOSENDCHANGING);
            _pendingPhysX = x;
            _pendingPhysY = y;
            _lastAppliedPhysX = x;
            _lastAppliedPhysY = y;
            RememberDockSnapshot(ownerRect, popupW, popupH);
        }
        finally
        {
            _isOurSetWindowPosForPicker = false;
            sw.Stop();
            if (sw.ElapsedMilliseconds >= 8 && _perfDockFollowSlowLogCount < 60)
            {
                _perfDockFollowSlowLogCount++;
                ClipboardDiagnosticsLog.Write(
                    $"filejump.perf realtime_dock_follow elapsedMs={sw.ElapsedMilliseconds} force={force} x={x} y={y}");
            }
        }
    }

    private void UpdateDockPopupPhysicalSizeCache()
    {
        if (!_dockBesideDialog) return;

        try
        {
            var wpfW = ActualWidth > 1 ? ActualWidth : Width;
            var wpfH = ActualHeight > 1 ? ActualHeight : MaxHeight > 1 && MaxHeight < 900 ? MaxHeight : Height;
            var dpiPoint = new Win32.POINT { X = _mouseScreenX, Y = _mouseScreenY };
            if (TryReadDockOwnerRect(out var ownerRect))
            {
                dpiPoint.X = (ownerRect.Left + ownerRect.Right) / 2;
                dpiPoint.Y = (ownerRect.Top + ownerRect.Bottom) / 2;
            }

            var hMon = Win32.MonitorFromPoint(dpiPoint, Win32.MONITOR_DEFAULTTONEAREST);
            Win32.GetDpiForMonitor(hMon, 0, out uint monDpiX, out uint monDpiY);
            _dockPopupPhysWidth = Math.Max(1, (int)Math.Ceiling(wpfW * (monDpiX / 96.0)));
            _dockPopupPhysHeight = Math.Max(1, (int)Math.Ceiling(wpfH * (monDpiY / 96.0)));
        }
        catch
        {
            // ignore
        }
    }

    private bool TryReadDockOwnerRect(out Win32.RECT ownerRect)
    {
        ownerRect = default;
        return _fileDialogOwnerHwnd != IntPtr.Zero
               && Win32.IsWindow(_fileDialogOwnerHwnd)
               && Win32.IsWindowVisible(_fileDialogOwnerHwnd)
               && Win32.GetWindowRect(_fileDialogOwnerHwnd, out ownerRect);
    }

    private void RememberDockSnapshot(Win32.RECT ownerRect, int actualWidth, int actualHeight)
    {
        _lastDockOwnerLeft = ownerRect.Left;
        _lastDockOwnerTop = ownerRect.Top;
        _lastDockOwnerRight = ownerRect.Right;
        _lastDockOwnerBottom = ownerRect.Bottom;
        _lastDockActualWidth = actualWidth;
        _lastDockActualHeight = actualHeight;
    }

    private void ResetDockSnapshot()
    {
        _lastDockOwnerLeft = int.MinValue;
        _lastDockOwnerTop = int.MinValue;
        _lastDockOwnerRight = int.MinValue;
        _lastDockOwnerBottom = int.MinValue;
        _lastDockActualWidth = int.MinValue;
        _lastDockActualHeight = int.MinValue;
    }

    private void InstallDockOwnerFollowHooks()
    {
        s_jumpPickerDockWinEventOwner = this;

        if (_dockOwnerMoveSizeHook == IntPtr.Zero)
        {
            _dockOwnerMoveSizeHook = Win32.SetWinEventHook(
                Win32.EVENT_SYSTEM_MOVESIZESTART,
                Win32.EVENT_SYSTEM_MOVESIZEEND,
                IntPtr.Zero,
                s_jumpPickerDockWinEventThunk,
                0,
                0,
                Win32.WINEVENT_OUTOFCONTEXT | Win32.WINEVENT_SKIPOWNPROCESS);
        }

        if (_dockOwnerLocationHook == IntPtr.Zero)
        {
            _dockOwnerLocationHook = Win32.SetWinEventHook(
                Win32.EVENT_OBJECT_LOCATIONCHANGE,
                Win32.EVENT_OBJECT_LOCATIONCHANGE,
                IntPtr.Zero,
                s_jumpPickerDockWinEventThunk,
                0,
                0,
                Win32.WINEVENT_OUTOFCONTEXT | Win32.WINEVENT_SKIPOWNPROCESS);
        }
    }

    private void UninstallDockOwnerFollowHooks()
    {
        if (_dockOwnerMoveSizeHook != IntPtr.Zero)
        {
            Win32.UnhookWinEvent(_dockOwnerMoveSizeHook);
            _dockOwnerMoveSizeHook = IntPtr.Zero;
        }

        if (_dockOwnerLocationHook != IntPtr.Zero)
        {
            Win32.UnhookWinEvent(_dockOwnerLocationHook);
            _dockOwnerLocationHook = IntPtr.Zero;
        }

        if (s_jumpPickerDockWinEventOwner == this)
            s_jumpPickerDockWinEventOwner = null;
    }

    private bool DockEventBelongsToOwner(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero || _fileDialogOwnerHwnd == IntPtr.Zero) return false;
        if (!Win32.IsWindow(_fileDialogOwnerHwnd)) return false;

        var ownerRoot = Win32.GetAncestor(_fileDialogOwnerHwnd, Win32.GA_ROOT);
        if (ownerRoot == IntPtr.Zero) return false;

        var eventRoot = Win32.GetAncestor(hwnd, Win32.GA_ROOT);
        if (eventRoot == IntPtr.Zero || eventRoot != ownerRoot) return false;
        return hwnd == ownerRoot || hwnd == _fileDialogOwnerHwnd;
    }
}
