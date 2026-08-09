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
{
    internal static bool TryClaimOwnerDestroyHook(ref IntPtr registeredHook, IntPtr callbackHook) =>
        callbackHook != IntPtr.Zero
        && Interlocked.CompareExchange(
            ref registeredHook,
            IntPtr.Zero,
            callbackHook) == callbackHook;

    private void InstallJumpPickerOutsideHooks()
    {
        if (_jumpPickerMouseHook != IntPtr.Zero) return;
        s_jumpPickerMouseOwner = this;
        ClipboardManager.Services.GlobalHookDispatcher.Dispatcher.Invoke(() =>
        {
            _jumpPickerMouseHook = Win32.SetWindowsHookEx(
                Win32.WH_MOUSE_LL, s_jumpPickerMouseThunk, Win32.GetModuleHandle(null), 0);
            s_jumpPickerMouseHookForNext = _jumpPickerMouseHook;
        });

        if (_jumpPickerWinEventHook == IntPtr.Zero)
        {
            s_jumpPickerWinEventOwner = this;
            _jumpPickerWinEventHook = Win32.SetWinEventHook(
                Win32.EVENT_SYSTEM_FOREGROUND, Win32.EVENT_SYSTEM_FOREGROUND,
                IntPtr.Zero, s_jumpPickerWinEventThunk, 0, 0,
                Win32.WINEVENT_OUTOFCONTEXT | Win32.WINEVENT_SKIPOWNPROCESS);
        }
    }

    private void UninstallJumpPickerOutsideHooks()
    {
        if (_jumpPickerMouseHook != IntPtr.Zero)
        {
            var hk = _jumpPickerMouseHook;
            _jumpPickerMouseHook = IntPtr.Zero;
            ClipboardManager.Services.GlobalHookDispatcher.Dispatcher.Invoke(() => Win32.UnhookWindowsHookEx(hk));
        }
        if (s_jumpPickerMouseOwner == this)
        {
            s_jumpPickerMouseOwner = null;
            s_jumpPickerMouseHookForNext = IntPtr.Zero;
        }

        if (_jumpPickerWinEventHook != IntPtr.Zero)
        {
            Win32.UnhookWinEvent(_jumpPickerWinEventHook);
            _jumpPickerWinEventHook = IntPtr.Zero;
        }
        if (s_jumpPickerWinEventOwner == this)
            s_jumpPickerWinEventOwner = null;
    }

    private static IntPtr StaticJumpPickerMouseHook(int nCode, IntPtr wParam, IntPtr lParam)
    {
        var owner = s_jumpPickerMouseOwner;
        var hhk = s_jumpPickerMouseHookForNext;
        if (owner != null && hhk != IntPtr.Zero)
        {
            try { return owner.JumpPickerMouseHookProc(nCode, wParam, lParam); }
            catch (Exception ex) { ClipboardDiagnosticsLog.Write($"native jump-picker mouse hook exception: {ex}"); }
        }
        return Win32.CallNextHookEx(hhk, nCode, wParam, lParam);
    }

    private static void StaticJumpPickerWinEventProc(
        IntPtr hWinEventHook, uint eventType, IntPtr hwnd,
        int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
    {
        var owner = s_jumpPickerWinEventOwner;
        if (owner == null) return;
        try
        {
            owner.JumpPickerForegroundCallback(hWinEventHook, eventType, hwnd, idObject, idChild, dwEventThread, dwmsEventTime);
        }
        catch (Exception ex)
        {
            ClipboardDiagnosticsLog.Write($"native jump-picker foreground event exception: {ex}");
        }
    }

    private static void StaticJumpPickerDockWinEventProc(
        IntPtr hWinEventHook, uint eventType, IntPtr hwnd,
        int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
    {
        var owner = s_jumpPickerDockWinEventOwner;
        if (owner == null) return;
        try
        {
            owner.JumpPickerDockMoveSizeCallback(eventType, hwnd, idObject, idChild);
        }
        catch (Exception ex)
        {
            ClipboardDiagnosticsLog.Write($"native jump-picker dock event exception: {ex}");
        }
    }

    private static void StaticJumpPickerOwnerDestroyProc(
        IntPtr hWinEventHook, uint eventType, IntPtr hwnd,
        int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
    {
        // 只关心整个窗口本身被销毁（忽略滚动条、光标等内部对象的销毁事件，否则会导致误关面板）
        if (idObject != Win32.OBJID_WINDOW || idChild != 0) return;

        try
        {
            var owner = s_jumpPickerOwnerDestroyOwner;
            if (owner == null) return;
            // ClipboardX 历史面板只暂停外部点击/前台切换关闭；宿主文件对话框销毁时仍必须关闭跳转面板。
            if (owner._suppressDismissForSubDialog) return;
            var ownerDestroyed = owner._fileDialogOwnerHwnd == IntPtr.Zero
                                 || hwnd == owner._fileDialogOwnerHwnd
                                 || !Win32.IsWindow(owner._fileDialogOwnerHwnd);
            if (!ownerDestroyed) return;

            // 必须先原子认领再真正 Unhook。旧实现先把字段清零，Closed 清理因而看不到句柄，
            // 每次 owner 销毁都会永久泄漏一个 WinEvent hook；常驻后同一事件会回调数百次。
            // hWinEventHook 不等于当前实例句柄时说明这是旧/重复回调，不能关闭当前 picker。
            if (!TryClaimOwnerDestroyHook(ref owner._ownerDestroyHook, hWinEventHook))
                return;

            try { Win32.UnhookWinEvent(hWinEventHook); } catch { }
            if (ReferenceEquals(s_jumpPickerOwnerDestroyOwner, owner))
                s_jumpPickerOwnerDestroyOwner = null;

            owner.Dispatcher.BeginInvoke(() =>
            {
                ShellNavigateLog.Write(
                    "filejump",
                    owner._fileDialogOwnerHwnd == IntPtr.Zero
                        ? "Picker Closed: StaticJumpPickerOwnerDestroyProc (No owner)"
                        : $"Picker Closed: StaticJumpPickerOwnerDestroyProc (hwnd match: {hwnd:X})");
                try { owner.Close(); } catch { }
            });
        }
        catch (Exception ex)
        {
            ClipboardDiagnosticsLog.Write($"native jump-picker owner destroy event exception: {ex}");
        }
    }

    private void InstallOwnerDestroyHook()
    {
        if (_fileDialogOwnerHwnd == IntPtr.Zero) return;
        s_jumpPickerOwnerDestroyOwner = this;
        _ownerDestroyHook = Win32.SetWinEventHook(
            Win32.EVENT_OBJECT_DESTROY, Win32.EVENT_OBJECT_DESTROY,
            IntPtr.Zero, s_jumpPickerOwnerDestroyThunk,
            0, 0,
            Win32.WINEVENT_OUTOFCONTEXT | Win32.WINEVENT_SKIPOWNPROCESS);
    }

    private void UninstallOwnerDestroyHook()
    {
        if (ReferenceEquals(s_jumpPickerOwnerDestroyOwner, this))
            s_jumpPickerOwnerDestroyOwner = null;
        var hook = Interlocked.Exchange(ref _ownerDestroyHook, IntPtr.Zero);
        if (hook == IntPtr.Zero) return;
        try { Win32.UnhookWinEvent(hook); } catch { }
    }

    private IntPtr JumpPickerMouseHookProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && _isPickerReadyForMouseHook)
        {
            var msg = wParam.ToInt32();
            if (msg is Win32.WM_LBUTTONDOWN or Win32.WM_RBUTTONDOWN)
            {
                if (_settings.HideOnSameAppClick)
                {
                    var info = Marshal.PtrToStructure<Win32.MSLLHOOKSTRUCT>(lParam);
                    var clickHwnd = Win32.WindowFromPoint(info.pt);
                    var rootHwnd = clickHwnd != IntPtr.Zero ? Win32.GetAncestor(clickHwnd, Win32.GA_ROOT) : IntPtr.Zero;
                    if (rootHwnd == _hwnd)
                    {
                        _clickReceivedByJumpPicker = true;
                    }
                    else
                    {
                        _clickReceivedByJumpPicker = false;
                        Dispatcher.BeginInvoke(
                            DispatcherPriority.Background,
                            (Action)(() => TryDismissJumpPickerFromOutsideMouse()));
                    }
                }
            }
        }
        return Win32.CallNextHookEx(_jumpPickerMouseHook, nCode, wParam, lParam);
    }

    private void JumpPickerForegroundCallback(
        IntPtr hWinEventHook, uint eventType, IntPtr hwnd,
        int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
    {
        Dispatcher.BeginInvoke(() => TryDismissJumpPickerFromForegroundChange(hwnd));
    }

    private void JumpPickerDockMoveSizeCallback(uint eventType, IntPtr hwnd, int idObject, int idChild)
    {
        if (idObject != Win32.OBJID_WINDOW || idChild != 0) return;
        if (!DockEventBelongsToOwner(hwnd)) return;

        if (eventType == Win32.EVENT_OBJECT_LOCATIONCHANGE)
        {
            TryRealtimeDockFollow();
            return;
        }

        if (eventType == Win32.EVENT_SYSTEM_MOVESIZESTART)
        {
            _dockOwnerMoveActive = true;
            return;
        }

        if (eventType == Win32.EVENT_SYSTEM_MOVESIZEEND)
        {
            _dockOwnerMoveActive = false;
            TryRealtimeDockFollow(force: true);
            FlushDeferredExternalRefresh();
        }
    }

    private void TryDismissJumpPickerFromOutsideMouse()
    {
        if (!IsLoaded || Opacity <= 0) return;
        if (Environment.TickCount64 - _loadedTick < 150) return;
        if (_clickReceivedByJumpPicker) return;
        if (JumpRowContextMenu.IsOpen) return;
        if (IsDismissSuppressed) return;
        // 跳转窗是 modeless 窗口，关闭只负责收起 UI，导航由 CommitNavigateKeepOpen 独立完成。
        ShellNavigateLog.Write("filejump", "Picker Closed: TryDismissJumpPickerFromOutsideMouse");
        Close();
    }

    private void TryDismissJumpPickerFromForegroundChange(IntPtr newForeground)
    {
        if (!IsLoaded || Opacity <= 0) return;
        if (Environment.TickCount64 - _loadedTick < 150) return;
        if (newForeground == _hwnd) return;
        if (ForegroundOverlayPolicy.ShouldIgnoreForegroundWindow(newForeground)) return;

        // 全局独立模式：面板本身是 WS_EX_NOACTIVATE，永远不持有前台；
        // 关闭只由 Esc / Enter / Click 显式触发，不因前台切换而关闭。
        // （若正在等待目标窗口重新前台，该逻辑在 WinEvent 回调中处理。）
        if (_isStandaloneMode) return;

        // 忽略前台切换过程中的瞬间空窗口状态，避免误关
        if (newForeground == IntPtr.Zero) return;

        // 新前台是任何文件对话框时不关闭——跳转面板的存在意义就是服务文件对话框，
        // 无论是否为当前 owner，都应保持面板让用户有机会选择路径。
        if (newForeground != IntPtr.Zero)
        {
            var resolvedDialog = FileDialogJumpHelper.ResolveFileDialogHwndFromWindowOrAncestor(newForeground);
            if (resolvedDialog != IntPtr.Zero)
            {
                // 如果宿主抢走焦点，尽量抢回来，保证打字能输入到搜索框（即使用户点出了弹窗也不影响焦点霸权）
                if (_autoForegroundStickyMode)
                {
                    Dispatcher.BeginInvoke(TryStealFocusForPicker, DispatcherPriority.Input);
                }
                return;
            }
        }

        // 贴靠模式额外保护：若新前台属于 owner 对话框的根窗口也不关。
        // 注意：必须用 GA_ROOTOWNER（3），因为宿主主窗（如 Antigravity）与它弹出的模态对话框属于同一 Owner 树，
        // 如果只用 GA_ROOT，当点击主窗时会误判而导致面板退出（Issue #2 实际上被遮住了或退出了）。
        if (_autoForegroundStickyMode && newForeground != IntPtr.Zero && _fileDialogOwnerHwnd != IntPtr.Zero)
        {
            var newRoot = Win32.GetAncestor(newForeground, 3 /* GA_ROOTOWNER */);
            var ownerRoot = Win32.GetAncestor(_fileDialogOwnerHwnd, 3 /* GA_ROOTOWNER */);
            if (newRoot == ownerRoot) return;
        }

        Win32.GetCursorPos(out var cursor);
        if (Win32.WindowFromPoint(cursor) == _hwnd) return;
        if (JumpRowContextMenu.IsOpen) return;
        if (IsDismissSuppressed) return;
        ShellNavigateLog.Write("filejump", $"Picker Closed: TryDismissJumpPickerFromForegroundChange. newForeground={newForeground:X}");
        Close();
    }
}

