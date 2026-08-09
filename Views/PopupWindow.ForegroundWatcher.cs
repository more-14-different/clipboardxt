using System;
using System.Diagnostics;
using System.Threading;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace ClipboardManager;

public partial class PopupWindow : Window
{
    private static void StaticWinEventProc(
        IntPtr hWinEventHook, uint eventType, IntPtr hwnd,
        int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
    {
        var owner = s_popupWinEventOwner;
        if (owner == null) return;
        try
        {
            if (eventType == Win32.EVENT_SYSTEM_FOREGROUND)
                owner.OnForegroundChanged(hWinEventHook, eventType, hwnd, idObject, idChild, dwEventThread, dwmsEventTime);
            else if (eventType == Win32.EVENT_OBJECT_FOCUS)
                owner.OnGlobalFocusMaybeFileDialog(hwnd);
        }
        catch (Exception ex)
        {
            ClipboardDiagnosticsLog.Write($"native foreground event exception: {ex}");
        }
    }

    private void InstallForegroundWatcher()
    {
        if (_winEventHook != IntPtr.Zero) return;
        s_popupWinEventOwner = this;
        _winEventHook = Win32.SetWinEventHook(
            Win32.EVENT_SYSTEM_FOREGROUND, Win32.EVENT_SYSTEM_FOREGROUND,
            IntPtr.Zero, s_popupWinEventThunk, 0, 0,
            Win32.WINEVENT_OUTOFCONTEXT | Win32.WINEVENT_SKIPOWNPROCESS);
        if (_winEventHook == IntPtr.Zero)
        {
            s_popupWinEventOwner = null;
            return;
        }

        _winEventHookFocus = Win32.SetWinEventHook(
            Win32.EVENT_OBJECT_FOCUS, Win32.EVENT_OBJECT_FOCUS,
            IntPtr.Zero, s_popupWinEventThunk, 0, 0,
            Win32.WINEVENT_OUTOFCONTEXT | Win32.WINEVENT_SKIPOWNPROCESS);
        if (_winEventHookFocus == IntPtr.Zero)
        {
            Win32.UnhookWinEvent(_winEventHook);
            _winEventHook = IntPtr.Zero;
            s_popupWinEventOwner = null;
        }
    }

    private void UninstallForegroundWatcher()
    {
        if (_winEventHookFocus != IntPtr.Zero)
        {
            Win32.UnhookWinEvent(_winEventHookFocus);
            _winEventHookFocus = IntPtr.Zero;
        }
        if (_winEventHook != IntPtr.Zero)
        {
            Win32.UnhookWinEvent(_winEventHook);
            _winEventHook = IntPtr.Zero;
        }
        if (s_popupWinEventOwner == this)
            s_popupWinEventOwner = null;
    }

    /// <summary>
    /// 微信等打开「选择文件」时可能不发前台切换事件，但焦点会进对话框；与 <see cref="OnForegroundChanged"/> 共用自动弹逻辑。
    /// </summary>
    private void OnGlobalFocusMaybeFileDialog(IntPtr hwnd)
    {
        if (_appSettings == null) return;
        if (hwnd == IntPtr.Zero) return;
        if (!FileDialogJumpHelper.QuickMayBeUnderFileDialog(hwnd)) return;

        var dlg = FileDialogJumpHelper.ResolveFileDialogHwndFromWindowOrAncestor(hwnd);
        if (dlg == IntPtr.Zero) return;

        var runOpenList = _appSettings.FileJumpPickerOpenWhenDialogForeground;
        var runAutoNavigate = _appSettings.FileJumpAutoOnFirstClick;
        if (!runOpenList && !runAutoNavigate)
            return;

        if (runOpenList || runAutoNavigate)
            ScheduleSnapshotFolderFromDialog(dlg);

        Dispatcher.BeginInvoke(() =>
        {
            // 「自动弹列表」开：走列表路径（开+开时该路径内部会先直跳再弹列表）。
            // 仅「自动跳转」开：走纯直跳路径；同时武装鼠标钩，覆盖无前台事件的宿主。
            if (runOpenList)
                TryAutoOpenFileJumpPickerWhenDialogForeground(dlg);
            else if (runAutoNavigate)
                TryAutoNavigateBestPathWhenDialogForeground(dlg);

            // 仅在「自动跳转」开 + 「自动弹列表」关时才需要鼠标钩兜底（弹列表已由前台事件触发，无需点击）。
            if (runAutoNavigate && !runOpenList)
                UpdateFileJumpClickToNavigateArm(dlg);
        });
    }

    private void OnForegroundChanged(
        IntPtr hWinEventHook, uint eventType, IntPtr hwnd,
        int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
    {
        if (ForegroundOverlayPolicy.ShouldIgnoreForegroundWindow(hwnd)) return;

        Interlocked.Increment(ref _foregroundNativeBurst);
        var prev = _lastForegroundForDialogTrack;
        _lastForegroundForDialogTrack = hwnd;
        _sourceTracker.UpdateForeground(hwnd);

        int seq = Interlocked.Increment(ref _foregroundChangeCoalesceGen);
        var prevCap = prev;
        var hwndCap = hwnd;
        Dispatcher.BeginInvoke(DispatcherPriority.Background, () =>
        {
            if (seq != Volatile.Read(ref _foregroundChangeCoalesceGen))
            {
                Interlocked.Increment(ref _foregroundUiDispatchSuperseded);
                return;
            }

            int nat = Interlocked.Exchange(ref _foregroundNativeBurst, 0);
            int super = Interlocked.Exchange(ref _foregroundUiDispatchSuperseded, 0);
            ProcessForegroundChangedUi(prevCap, hwndCap, nat, super);
        });
    }

    /// <summary>
    /// 原 OnForegroundChanged 内多次 BeginInvoke 会在前台连发时撑爆队列（尤其关跳转列表瞬间），
    /// 与跳转窗共用同一 Dispatcher 时表现为长时间卡顿。
    /// </summary>
    private void ProcessForegroundChangedUi(IntPtr prev, IntPtr hwnd, int nativeBurst, int supersededDispatches)
    {
        var sw = Stopwatch.StartNew();

        // WinEvent 回调可能不在 WPF Dispatcher 线程；窗口句柄及 picker 状态统一在这里读取。
        if (_activeFileJumpPicker != null && hwnd != new WindowInteropHelper(_activeFileJumpPicker).Handle)
        {
            var ownerHwnd = _activeFileJumpPicker.OwnerDialogHwnd;
            if (ownerHwnd == IntPtr.Zero || !Win32.IsWindow(ownerHwnd))
            {
                try { _activeFileJumpPicker.Close(); } catch { }
                _activeFileJumpPicker = null;
                StopExplorerPathPoll();
            }
        }

        if (_activeFileJumpPicker == null)
        {
            TryRememberFolderFromDialog(prev);
        }
        ScheduleRememberExternalManagerFolders(prev, hwnd);

        if (_activeFileJumpPicker == null)
        {
            var dialogForForeground = FileDialogJumpHelper.ResolveFileDialogHwndFromWindowOrAncestor(hwnd);
            if (dialogForForeground != IntPtr.Zero)
            {
                _lastFileDialogSeenTick = Environment.TickCount64;
                var prevForAutoSync = prev;
                ScheduleSnapshotFolderFromDialog(dialogForForeground);
                var navigationSuppressed = IsFileJumpNavigationSuppressed(dialogForForeground);
                if (_appSettings != null && !navigationSuppressed)
                {
                    if (_appSettings.FileJumpPickerOpenWhenDialogForeground)
                        TryAutoOpenFileJumpPickerWhenDialogForeground(dialogForForeground);
                    else if (_appSettings.FileJumpAutoOnFirstClick)
                        TryAutoNavigateBestPathWhenDialogForeground(dialogForForeground);
                }

                if (!navigationSuppressed)
                    TryAutoSyncPathOnDialogReturn(hwnd, prevForAutoSync);
            }

            UpdateFileJumpClickToNavigateArm(
                dialogForForeground != IntPtr.Zero ? dialogForForeground : hwnd);
        }
        else
        {
            var dlgPick = FileDialogJumpHelper.ResolveFileDialogHwndFromWindowOrAncestor(hwnd);
            var dialogForForeground = dlgPick;

            // picker 打开时切回文件对话框 → 触发 auto-sync
            if (dialogForForeground != IntPtr.Zero)
            {
                if (!IsFileJumpNavigationSuppressed(dialogForForeground))
                    TryAutoSyncPathOnDialogReturn(hwnd, prev);
            }
            // picker 打开时切到外部管理器 → 触发采集刷新列表（新 Explorer 路径会被加入候选）
            else if (dialogForForeground == IntPtr.Zero && _activeFileJumpPicker != null)
                TryRefreshPickerForNewExternalFolder(hwnd);

            UpdateFileJumpClickToNavigateArm(dlgPick != IntPtr.Zero ? dlgPick : hwnd);
        }

        var shouldHidePopup = _isPopupVisible
            && !_popupPinned
            && !_isResizing
            && hwnd != _hwnd
            && hwnd != _targetWindow;
        if (shouldHidePopup)
        {
            Win32.GetCursorPos(out var cursor);
            if (Win32.WindowFromPoint(cursor) != _hwnd)
                HidePopup();
        }

        sw.Stop();
        int ms = (int)sw.ElapsedMilliseconds;
        bool pickerOpen = _activeFileJumpPicker != null;
        bool logFg = nativeBurst >= 2 || supersededDispatches > 0 || ms >= 15 || pickerOpen
            || _fileJumpPickerOpenInProgress || ms >= 40;
        if (logFg)
        {
            var slow = ms >= 40 ? " SLOW" : "";
            ShellNavigateLog.Write("filejump",
                $"fg_ui nat={nativeBurst} super={supersededDispatches} ms={ms} prev=0x{prev.ToInt64():X} hwnd=0x{hwnd.ToInt64():X} picker={pickerOpen} openInProg={_fileJumpPickerOpenInProgress}{slow}");
        }
    }
}
