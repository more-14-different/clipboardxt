using System.Runtime.InteropServices;
using System.Windows;

namespace ClipboardManager;

public partial class PopupWindow : Window
{
    /// <summary>
    /// 开始菜单/搜索等 Shell 前台时，光标与插入点位置不可靠，不跟随；固定在当前显示器工作区左上，减少与居中 Shell 重叠。
    /// </summary>
    private void PositionPopupFixedShellWorkArea()
    {
        System.Drawing.Rectangle work;
        if (_targetWindow != IntPtr.Zero && Win32.GetWindowRect(_targetWindow, out var fgRect))
        {
            int cx = (fgRect.Left + fgRect.Right) / 2;
            int cy = (fgRect.Top + fgRect.Bottom) / 2;
            work = System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point(cx, cy)).WorkingArea;
        }
        else
        {
            work = System.Windows.Forms.Screen.PrimaryScreen?.WorkingArea
                   ?? new System.Drawing.Rectangle(0, 0,
                       (int)SystemParameters.PrimaryScreenWidth,
                       (int)SystemParameters.PrimaryScreenHeight);
        }

        var hMon = Win32.MonitorFromPoint(
            new Win32.POINT { X = work.Left + work.Width / 2, Y = work.Top + work.Height / 2 },
            Win32.MONITOR_DEFAULTTONEAREST);
        Win32.GetDpiForMonitor(hMon, 0, out uint monDpiX, out uint monDpiY);
        double scaleX = monDpiX / 96.0;
        double scaleY = monDpiY / 96.0;

        var popupSize = PopupPlacementCalculator.ToPhysicalSize(
            Width,
            ActualHeight,
            MaxHeight,
            scaleX,
            scaleY);
        var position = PopupPlacementCalculator.PlaceFixedAtTopLeft(work, popupSize);

        _pendingPhysX = position.X;
        _pendingPhysY = position.Y;
    }

    private CaretAutomationProbe? StartCaretAutomationProbeIfUseful()
    {
        if (_isFileJumpSearchPasteRoutingActive
            && _fileJumpSearchPasteTarget?.TryGetSearchPasteScreenAnchor(out _) == true)
        {
            return null;
        }

        if (IsShellForegroundWindow(_targetWindow)
            || _popupPosition == "Mouse"
            || IsExplorerDesktopForeground(_targetWindow))
        {
            return null;
        }

        return StartCaretAutomationProbe();
    }

    private void PositionPopup(
        CaretAutomationProbe? caretAutomationProbe = null,
        bool allowCaretAutomation = true)
    {
        const int caretGap = 24;

        if (_isFileJumpSearchPasteRoutingActive
            && _fileJumpSearchPasteTarget?.TryGetSearchPasteScreenAnchor(out var fileJumpAnchor) == true)
        {
            SetPositionWithOffset(fileJumpAnchor.X, fileJumpAnchor.Y + caretGap);
            AgentDbgLog("H23", "PositionPopup", "branch=FileJumpSearchAnchor",
                new { fileJumpAnchor.X, fileJumpAnchor.Y, _pendingPhysX, _pendingPhysY });
            return;
        }

        if (IsShellForegroundWindow(_targetWindow))
        {
            PositionPopupFixedShellWorkArea();
            #region agent log
            AgentDbgLog("H23", "PositionPopup", "branch=ShellWorkArea",
                new { _targetWindow = _targetWindow.ToInt64(), _pendingPhysX, _pendingPhysY });
            #endregion
            return;
        }

        if (_popupPosition == "Mouse")
        {
            Win32.GetCursorPos(out var pt);
            SetPositionWithOffset(pt.X + 8, pt.Y + 20);
            #region agent log
            AgentDbgLog("H23", "PositionPopup", "branch=MousePref",
                new { pt.X, pt.Y, _pendingPhysX, _pendingPhysY });
            #endregion
            return;
        }

        // 资源管理器驱动的桌面（壁纸/图标层）：无可靠文本光标，跟随鼠标更符合直觉
        if (IsExplorerDesktopForeground(_targetWindow))
        {
            Win32.GetCursorPos(out var deskPt);
            SetPositionWithOffset(deskPt.X + 8, deskPt.Y + 20);
            #region agent log
            AgentDbgLog("H23", "PositionPopup", "branch=ExplorerDesktop",
                new { deskPt.X, deskPt.Y, _pendingPhysX, _pendingPhysY });
            #endregion
            return;
        }

        // UIA 优先（FocusedElement+TextPattern.Selection 在 Office/Chromium 上必然是文档真实 caret）；
        // 历史上把 GetGUIThreadInfo 放第一位会把 Word ribbon 上的 user32 Edit 控件 caret 当成「输入位置」，
        // 导致同一文档第二次呼出时焦点恢复到 ribbon 上的次级 Edit、弹窗错位到 ribbon 区域。
        if (allowCaretAutomation
            && TryGetCaretByAutomation(caretAutomationProbe, out double uiaX, out double uiaY))
        {
            SetPositionWithOffset(uiaX, uiaY + caretGap);
            CacheCaretSuccess(_targetWindow, _pendingPhysX, _pendingPhysY);
            #region agent log
            AgentDbgLog("H23", "PositionPopup", "branch=UIA",
                new { uiaX, uiaY, _pendingPhysX, _pendingPhysY });
            #endregion
            return;
        }

        var fgThread = Win32.GetWindowThreadProcessId(_targetWindow, out _);
        var gti = new Win32.GUITHREADINFO { cbSize = Marshal.SizeOf<Win32.GUITHREADINFO>() };

        if (Win32.GetGUIThreadInfo(fgThread, ref gti)
            && gti.hwndCaret != IntPtr.Zero
            && PopupPlacementCalculator.HasUsableNativeCaretBounds(
                gti.rcCaret.Left,
                gti.rcCaret.Top,
                gti.rcCaret.Right,
                gti.rcCaret.Bottom))
        {
            var pt = new Win32.POINT { X = gti.rcCaret.Left, Y = gti.rcCaret.Bottom };
            Win32.ClientToScreen(gti.hwndCaret, ref pt);
            if (pt.X > 0 || pt.Y > 0)
            {
                SetPositionWithOffset(pt.X, pt.Y + caretGap);
                CacheCaretSuccess(_targetWindow, _pendingPhysX, _pendingPhysY);
                #region agent log
                AgentDbgLog("H23", "PositionPopup", "branch=GUIThreadInfoCaret",
                    new { pt.X, pt.Y, _pendingPhysX, _pendingPhysY });
                #endregion
                return;
            }
        }

        try
        {
            var myThread = Win32.GetCurrentThreadId();
            Win32.AttachThreadInput(myThread, fgThread, true);
            try
            {
                var focusWnd = Win32.GetFocus();
                if (focusWnd != IntPtr.Zero && Win32.GetCaretPos(out var caretPos))
                {
                    if (caretPos.X != 0 || caretPos.Y != 0)
                    {
                        Win32.ClientToScreen(focusWnd, ref caretPos);
                        SetPositionWithOffset(caretPos.X, caretPos.Y + caretGap);
                        CacheCaretSuccess(_targetWindow, _pendingPhysX, _pendingPhysY);
                        #region agent log
                        AgentDbgLog("H23", "PositionPopup", "branch=AttachedGetCaretPos",
                            new { caretPos.X, caretPos.Y, _pendingPhysX, _pendingPhysY });
                        #endregion
                        return;
                    }
                }
            }
            finally { Win32.AttachThreadInput(myThread, fgThread, false); }
        }
        catch { }

        // UIA 冷启动/Word 自绘 caret 兜底：30s 内同窗口曾成功定位过则复用，避免每次呼出都贴到鼠标处
        if (TryUseCachedCaret(_targetWindow, out int cachedX, out int cachedY))
        {
            _pendingPhysX = cachedX;
            _pendingPhysY = cachedY;
            #region agent log
            AgentDbgLog("H23", "PositionPopup", "branch=CachedCaret",
                new { cachedX, cachedY, ageMs = Environment.TickCount64 - _lastCaretCacheTickMs });
            #endregion
            return;
        }

        Win32.GetCursorPos(out var cursor);
        SetPositionWithOffset(cursor.X + 8, cursor.Y + 20);
        #region agent log
        AgentDbgLog("H23", "PositionPopup", "branch=CursorFallback",
            new { cursor.X, cursor.Y, _pendingPhysX, _pendingPhysY });
        #endregion
    }

    private void CacheCaretSuccess(IntPtr hwnd, int physX, int physY)
    {
        _lastCaretCacheHwnd = hwnd;
        _lastCaretCachePhysX = physX;
        _lastCaretCachePhysY = physY;
        _lastCaretCacheTickMs = Environment.TickCount64;
    }

    private bool TryUseCachedCaret(IntPtr hwnd, out int physX, out int physY)
    {
        physX = physY = 0;
        if (_lastCaretCacheHwnd == IntPtr.Zero || hwnd != _lastCaretCacheHwnd) return false;
        if (Environment.TickCount64 - _lastCaretCacheTickMs > 30_000) return false;
        physX = _lastCaretCachePhysX;
        physY = _lastCaretCachePhysY;
        return true;
    }

    /// <summary>当前前台是否为 Windows 桌面（Progman/WorkerW），即点击壁纸或桌面图标时的焦点窗体。</summary>
    private static bool IsExplorerDesktopForeground(IntPtr foregroundHwnd)
    {
        if (foregroundHwnd == IntPtr.Zero) return false;
        var cls = Win32.GetWindowClassName(foregroundHwnd);
        return cls.Equals("Progman", StringComparison.OrdinalIgnoreCase)
               || cls.Equals("WorkerW", StringComparison.OrdinalIgnoreCase);
    }

    private void SetPositionWithOffset(double physX, double physY)
    {
        var monitorPoint = new Win32.POINT { X = (int)physX, Y = (int)physY };
        var hMon = Win32.MonitorFromPoint(
            monitorPoint,
            Win32.MONITOR_DEFAULTTONEAREST);

        // Anchor coordinates are physical pixels. WinForms Screen.WorkingArea can be
        // DPI-virtualized, so prefer the physical rcWork returned by Win32.
        System.Drawing.Rectangle work;
        var monitorInfo = new Win32.MONITORINFO
        {
            cbSize = Marshal.SizeOf<Win32.MONITORINFO>()
        };
        if (hMon != IntPtr.Zero && Win32.GetMonitorInfo(hMon, ref monitorInfo))
        {
            work = System.Drawing.Rectangle.FromLTRB(
                monitorInfo.rcWork.Left,
                monitorInfo.rcWork.Top,
                monitorInfo.rcWork.Right,
                monitorInfo.rcWork.Bottom);
        }
        else
        {
            work = System.Windows.Forms.Screen.FromPoint(
                new System.Drawing.Point(monitorPoint.X, monitorPoint.Y)).WorkingArea;
        }

        uint monDpiX = 96;
        uint monDpiY = 96;
        if (hMon != IntPtr.Zero
            && Win32.GetDpiForMonitor(hMon, 0, out var detectedDpiX, out var detectedDpiY) == 0
            && detectedDpiX > 0
            && detectedDpiY > 0)
        {
            monDpiX = detectedDpiX;
            monDpiY = detectedDpiY;
        }
        double scaleX = monDpiX / 96.0;
        double scaleY = monDpiY / 96.0;

        var popupSize = PopupPlacementCalculator.ToPhysicalSize(
            Width,
            ActualHeight,
            MaxHeight,
            scaleX,
            scaleY);
        var position = PopupPlacementCalculator.PlaceNearAnchor(
            work,
            popupSize,
            physX,
            physY);

        _pendingPhysX = position.X;
        _pendingPhysY = position.Y;
    }
}

