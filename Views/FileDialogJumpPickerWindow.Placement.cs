using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using ClipboardManager.Models;
using Media = System.Windows.Media;
using Orientation = System.Windows.Controls.Orientation;

namespace ClipboardManager;

public partial class FileDialogJumpPickerWindow : Window
{    private void FileDialogJumpPickerWindow_SourceInitialized(object? sender, EventArgs e)
    {
        _loadedTick = Environment.TickCount64;
        try
        {
            var helper = new WindowInteropHelper(this);
            helper.EnsureHandle();
            _hwnd = helper.Handle;
            // 粘性贴靠模式若设为文件对话框的 Owned 窗口，系统常把前台/键盘留在宿主对话框，SetForeground 难以生效；本窗已 Topmost。
            // 实际上无论是自动贴靠还是手动唤出，跨进程设置 Owner 都会导致 WPF 失去焦点/MouseUp事件丢失，因此统一不设 Owner。
            // if (_fileDialogOwnerHwnd != IntPtr.Zero && !_autoForegroundStickyMode)
            //     helper.Owner = _fileDialogOwnerHwnd;

            // 全局独立模式：不抢焦点，对齐 Ctrl+Alt+V 剪贴板面板的 WS_EX_NOACTIVATE 行为。
            if (_isStandaloneMode)
            {
                var ex = Win32.GetWindowLongPtr(_hwnd, Win32.GWL_EXSTYLE);
                Win32.SetWindowLongPtr(_hwnd, Win32.GWL_EXSTYLE,
                    new IntPtr(ex.ToInt64() | Win32.WS_EX_NOACTIVATE | Win32.WS_EX_TOOLWINDOW));
            }

            HwndSource.FromHwnd(_hwnd)?.AddHook(JumpPickerWndProc);
        }
        catch { /* ignore */ }

        _lockJumpPickerNomove = true;
        try
        {
            ComputePhysicalPosition(useActualSize: false);
            ApplyPendingPhysicalAsWpfLeftTop();
            // 全局独立模式：首次定位用 SWP_NOACTIVATE，避免弹出时切走原窗口焦点。
            ApplyPendingPhysicalSetWindowPos(noActivate: _isStandaloneMode);
            ApplyPendingPhysicalAsWpfLeftTop();
        }
        catch { /* ignore */ }

        InstallKeyboardHook();
    }

    private IntPtr JumpPickerWndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        switch (msg)
        {
            case Win32.WM_MOUSEACTIVATE:
                // 全局独立模式：鼠标点击面板不激活（不夺走原窗口焦点）。
                if (_isStandaloneMode) { handled = true; return new IntPtr(Win32.MA_NOACTIVATE); }
                break;

            case Win32.WM_NCHITTEST:
                var htResult = WindowResizeHelper.HandleNcHitTest(_hwnd, lParam, 16, ref handled);
                if (handled) return htResult;
                break;

            case Win32.WM_SIZING:
                if (WindowResizeHelper.HandleWmSizing(_hwnd, wParam, lParam,
                    MinWidth > 0 ? MinWidth : 280, 1200,
                    MinHeight > 0 ? MinHeight : 200, MaxHeight > 0 ? MaxHeight : 900,
                    _userHasResized))
                {
                    _userHasResized = true;
                    Dispatcher.BeginInvoke(() => SizeToContent = SizeToContent.Manual);
                }
                _isResizing = true;
                var sizingRc = Marshal.PtrToStructure<Win32.RECT>(lParam);
                Dispatcher.BeginInvoke(() =>
                {
                    var src = HwndSource.FromHwnd(_hwnd);
                    double sx = src?.CompositionTarget != null ? src.CompositionTarget.TransformFromDevice.M11 : 1;
                    double sy = src?.CompositionTarget != null ? src.CompositionTarget.TransformFromDevice.M22 : 1;
                    double w = (sizingRc.Right - sizingRc.Left) * sx;
                    double h = (sizingRc.Bottom - sizingRc.Top) * sy;
                    if (w > 0 && h > 0)
                    {
                        Width = w;
                        Height = h;
                        MaxHeight = Math.Max(MaxHeight, h);
                        UpdateDockPopupPhysicalSizeCache();
                    }
                });
                handled = true;
                return IntPtr.Zero;

            case Win32.WM_ENTERSIZEMOVE:
                _isResizing = true;
                break;

            case Win32.WM_EXITSIZEMOVE:
                _isResizing = false;
                if (_settings != null)
                {
                    _settings.FileJumpPickerWidth = Width;
                    _settings.FileJumpPickerMaxHeight = MaxHeight;
                    if (_userHasResized && ActualHeight > 0)
                        _settings.FileJumpPickerHeight = ActualHeight;
                    _settings.Save();
                }
                break;

            case Win32.WM_DPICHANGED:
                _isOurSetWindowPosForPicker = false;
                break;
            case Win32.WM_WINDOWPOSCHANGING:
                if (!_isOurSetWindowPosForPicker && !_isResizing && _lockJumpPickerNomove)
                {
                    var pos = Marshal.PtrToStructure<Win32.WINDOWPOS>(lParam);
                    pos.flags |= Win32.SWP_NOMOVE;
                    Marshal.StructureToPtr(pos, lParam, false);
                }
                break;
        }
        return IntPtr.Zero;
    }

    private void ApplyPendingPhysicalSetWindowPos(bool noActivate = true)
    {
        if (_hwnd == IntPtr.Zero) return;
        _isOurSetWindowPosForPicker = true;
        try
        {
            var flags = Win32.SWP_NOSIZE | Win32.SWP_NOZORDER;
            if (noActivate) flags |= Win32.SWP_NOACTIVATE;
            Win32.SetWindowPos(_hwnd, IntPtr.Zero, _pendingPhysX, _pendingPhysY, 0, 0, flags);
        }
        finally
        {
            _isOurSetWindowPosForPicker = false;
        }
    }

    private void FileDialogJumpPickerWindow_ContentRendered(object? sender, EventArgs e)
    {
        if (_snappedPhysicalOnce) return;
        _snappedPhysicalOnce = true;

        InstallOwnerDestroyHook();

        var swTotal = Stopwatch.StartNew();

        try
        {
            var sw = Stopwatch.StartNew();
            UpdateLayout();
            PerfLog("content_rendered_update_layout", sw.ElapsedMilliseconds, 16);
            sw.Restart();
            UpdateDockPopupPhysicalSizeCache();
            PerfLog("content_rendered_size_cache", sw.ElapsedMilliseconds, 8);
            sw.Restart();
            ComputePhysicalPosition(useActualSize: true);
            PerfLog("content_rendered_compute_position", sw.ElapsedMilliseconds, 8);
            sw.Restart();
            // 首次布局完成：允许 SetWindowPos 顺带激活，避免 SWP_NOACTIVATE 与「Owned 子窗」叠加导致永远无法抢前台。
            ApplyPendingPhysicalSetWindowPos(noActivate: false);
            PerfLog("content_rendered_set_window_pos", sw.ElapsedMilliseconds, 8);
            _lastAppliedPhysX = _pendingPhysX;
            _lastAppliedPhysY = _pendingPhysY;
            sw.Restart();
            ApplyPendingPhysicalAsWpfLeftTop();
            PerfLog("content_rendered_apply_wpf_left_top", sw.ElapsedMilliseconds, 8);
        }
        catch { /* ignore */ }
        finally
        {
            _lockJumpPickerNomove = false;
        }

        Opacity = 1.0;
        _isPickerReadyForMouseHook = true;
        if (!_autoForegroundStickyMode)
            InstallJumpPickerOutsideHooks();
        if (_dockBesideDialog)
        {
            InstallDockOwnerFollowHooks();
            // WinEvent 提供实时跟随；timer 只兜底处理个别宿主不发 LOCATIONCHANGE 的场景。
            _dockFollowTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _dockFollowTimer.Tick += (_, _) => DockFollowTick();
            _dockFollowTimer.Start();
        }
        // 弹出后稍后再抢焦点，避免刚开窗立即拖动文件对话框时与系统拖动循环抢前台。
        // 全局独立模式不抢焦点（WS_EX_NOACTIVATE），键盘通过低级钩子路由到本面板。
        if (!_isStandaloneMode)
            ScheduleInitialFocusSteal();
        swTotal.Stop();
        PerfLog("content_rendered_total", swTotal.ElapsedMilliseconds, 30,
            $"dock={_dockBesideDialog} sticky={_autoForegroundStickyMode}");
    }

    private void ScheduleInitialFocusSteal()
    {
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(40) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            if (!IsLoaded || _dockOwnerMoveActive) return;
            TryStealFocusForPicker();
        };
        timer.Start();
    }

    private void ComputePhysicalPosition(bool useActualSize)
    {
        if (_dockBesideDialog
            && _fileDialogOwnerHwnd != IntPtr.Zero
            && Win32.IsWindow(_fileDialogOwnerHwnd)
            && TryApplyDockedPhysical(useActualSize))
            return;

        const int marginX = 14;
        const int marginY = 10;

        var screen = System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point(_mouseScreenX, _mouseScreenY));
        var work = screen.WorkingArea;

        var hMon = Win32.MonitorFromPoint(
            new Win32.POINT { X = _mouseScreenX, Y = _mouseScreenY },
            Win32.MONITOR_DEFAULTTONEAREST);
        Win32.GetDpiForMonitor(hMon, 0, out uint monDpiX, out uint monDpiY);
        double scaleX = monDpiX / 96.0;
        double scaleY = monDpiY / 96.0;

        double wpfW = useActualSize && ActualWidth > 1 ? ActualWidth : Width;
        double wpfH;
        if (useActualSize && ActualHeight > 1)
            wpfH = ActualHeight;
        else
            wpfH = MaxHeight > 1 && MaxHeight < 900 ? MaxHeight : 400;

        int popupW = (int)Math.Ceiling(wpfW * scaleX);
        int popupH = (int)Math.Ceiling(wpfH * scaleY);

        int x = _mouseScreenX + marginX;
        int y = _mouseScreenY + marginY;

        if (x + popupW > work.Right) x = work.Right - popupW;
        if (y + popupH > work.Bottom) y = _mouseScreenY - popupH - marginY;
        if (x < work.Left) x = work.Left;
        if (y < work.Top) y = work.Top;

        _pendingPhysX = x;
        _pendingPhysY = y;
    }

    /// <summary>按文件对话框计算紧贴物理坐标；失败则返回 false，由调用方回退到鼠标定位。</summary>
    private bool TryApplyDockedPhysical(bool useActualSize)
    {
        Win32.POINT dpiPt = new() { X = _mouseScreenX, Y = _mouseScreenY };
        if (Win32.GetWindowRect(_fileDialogOwnerHwnd, out var drDlg))
        {
            dpiPt.X = (drDlg.Left + drDlg.Right) / 2;
            dpiPt.Y = (drDlg.Top + drDlg.Bottom) / 2;
        }
        var hMon = Win32.MonitorFromPoint(dpiPt, Win32.MONITOR_DEFAULTTONEAREST);
        Win32.GetDpiForMonitor(hMon, 0, out uint monDpiX, out uint monDpiY);
        double scaleX = monDpiX / 96.0;
        double scaleY = monDpiY / 96.0;

        double wpfW = useActualSize && ActualWidth > 1 ? ActualWidth : Width;
        double wpfH;
        if (useActualSize && ActualHeight > 1)
            wpfH = ActualHeight;
        else
            wpfH = MaxHeight > 1 && MaxHeight < 900 ? MaxHeight : 400;

        int popupW = (int)Math.Ceiling(wpfW * scaleX);
        int popupH = (int)Math.Ceiling(wpfH * scaleY);

        if (!FileJumpPickerDockPlacement.TryComputePosition(_fileDialogOwnerHwnd, popupW, popupH, out var px, out var py))
            return false;

        _pendingPhysX = px;
        _pendingPhysY = py;
        return true;
    }

    private void ApplyPendingPhysicalAsWpfLeftTop()
    {
        try
        {
            var helper = new WindowInteropHelper(this);
            helper.EnsureHandle();
            var src = HwndSource.FromHwnd(helper.Handle);
            if (src?.CompositionTarget == null) return;

            var dip = src.CompositionTarget.TransformFromDevice.Transform(
                new System.Windows.Point(_pendingPhysX, _pendingPhysY));
            Left = dip.X;
            Top = dip.Y;
        }
        catch { /* ignore */ }
    }
}

