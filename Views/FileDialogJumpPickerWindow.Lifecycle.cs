using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;

namespace ClipboardManager;

public partial class FileDialogJumpPickerWindow
{
    private void FileDialogJumpPickerWindow_Closed(object? sender, EventArgs e)
    {
        // 先释放全局键盘钩子；设置持久化或其它清理稍慢时，也不能继续吞掉宿主的 Enter。
        UninstallKeyboardHook();
        if (_settings != null)
        {
            _settings.PanelOperationStates.FileJumpPicker = _settings.RememberPanelOperationState
                ? new PanelSearchOperationState(
                    _searchEditor.Text,
                    _searchEditor.CaretIndex,
                    _searchEditor.SelectionAnchor,
                    (int)_filterMode,
                    (ItemsList.SelectedItem as FileJumpPickerRow)?.Path)
                : null;
            _settings.FileJumpPickerWidth = Width;
            _settings.FileJumpPickerMaxHeight = MaxHeight;
            if (_userHasResized && ActualHeight > 0)
                _settings.FileJumpPickerHeight = ActualHeight;
            _settings.Save();
        }
        _everythingQueryCts?.Cancel();
        _everythingQueryCts = null;
        _dockFollowTimer?.Stop();
        _dockFollowTimer = null;
        _focusRetryTimer?.Stop();
        _focusRetryTimer = null;
        _searchRefreshTimer?.Stop();
        _searchRefreshTimer = null;
        _deferredExternalRefreshTimer?.Stop();
        _deferredExternalRefreshTimer = null;
        UninstallDockOwnerFollowHooks();
        UninstallJumpPickerOutsideHooks();
        UninstallOwnerDestroyHook();
        if (_hwnd != IntPtr.Zero)
        {
            try
            {
                HwndSource.FromHwnd(_hwnd)?.RemoveHook(JumpPickerWndProc);
            }
            catch { /* ignore */ }
        }
        if (_restoreOwnerFocusOnClose)
            RestoreOwnerFocusAfterEscape();
    }

    private static IntPtr CaptureOwnerFocusBeforePickerActivation(IntPtr ownerHwnd)
    {
        if (ownerHwnd == IntPtr.Zero || !Win32.IsWindow(ownerHwnd))
            return IntPtr.Zero;

        var ownerThread = Win32.GetWindowThreadProcessId(ownerHwnd, out _);
        if (ownerThread == 0)
            return IntPtr.Zero;

        var info = new Win32.GUITHREADINFO
        {
            cbSize = Marshal.SizeOf<Win32.GUITHREADINFO>()
        };
        if (!Win32.GetGUIThreadInfo(ownerThread, ref info))
            return IntPtr.Zero;

        return FocusHwndBelongsToOwner(info.hwndFocus, ownerHwnd)
            ? info.hwndFocus
            : IntPtr.Zero;
    }

    private void RestoreOwnerFocusAfterEscape()
    {
        if (_fileDialogOwnerHwnd == IntPtr.Zero || !Win32.IsWindow(_fileDialogOwnerHwnd))
            return;

        var ownerRoot = Win32.GetAncestor(_fileDialogOwnerHwnd, Win32.GA_ROOT);
        if (ownerRoot == IntPtr.Zero || !Win32.IsWindow(ownerRoot))
            return;

        var ownerThread = Win32.GetWindowThreadProcessId(_fileDialogOwnerHwnd, out _);
        if (ownerThread == 0)
            return;

        var focusHwnd = _ownerFocusHwndBeforePickerActivation;
        if (!FocusHwndBelongsToOwner(focusHwnd, _fileDialogOwnerHwnd))
        {
            var info = new Win32.GUITHREADINFO
            {
                cbSize = Marshal.SizeOf<Win32.GUITHREADINFO>()
            };
            focusHwnd = Win32.GetGUIThreadInfo(ownerThread, ref info)
                && FocusHwndBelongsToOwner(info.hwndFocus, _fileDialogOwnerHwnd)
                    ? info.hwndFocus
                    : _fileDialogOwnerHwnd;
        }

        var currentThread = Win32.GetCurrentThreadId();
        var attached = false;
        try
        {
            if (currentThread != ownerThread)
                attached = Win32.AttachThreadInput(currentThread, ownerThread, true);

            Win32.SetForegroundWindow(ownerRoot);
            if (focusHwnd != IntPtr.Zero && Win32.IsWindow(focusHwnd))
                Win32.SetFocus(focusHwnd);
        }
        catch
        {
            // 关闭面板不能因宿主正在销毁或切换焦点而失败。
        }
        finally
        {
            if (attached)
                Win32.AttachThreadInput(currentThread, ownerThread, false);
        }
    }

    private static bool FocusHwndBelongsToOwner(IntPtr focusHwnd, IntPtr ownerHwnd)
    {
        if (focusHwnd == IntPtr.Zero || !Win32.IsWindow(focusHwnd)
            || ownerHwnd == IntPtr.Zero || !Win32.IsWindow(ownerHwnd))
            return false;

        var focusRoot = Win32.GetAncestor(focusHwnd, Win32.GA_ROOT);
        var ownerRoot = Win32.GetAncestor(ownerHwnd, Win32.GA_ROOT);
        return focusRoot != IntPtr.Zero && focusRoot == ownerRoot;
    }

    private void FileDialogJumpPickerWindow_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateDockPopupPhysicalSizeCache();
    }

    private void FileDialogJumpPickerWindow_Activated(object? sender, EventArgs e)
    {
        if (IsLoaded)
            Dispatcher.BeginInvoke(TryStealFocusForPicker, DispatcherPriority.Input);
    }

    private void TryStealFocusForPicker()
    {
        if (_dockOwnerMoveActive || IsPrimaryMouseButtonDown())
        {
            ScheduleFocusRetry();
            return;
        }
        _focusRetryTimer?.Stop();
        _focusRetryTimer = null;
        _focusRetryCount = 0;
        var swTotal = Stopwatch.StartNew();
        try
        {
            var hwnd = _hwnd != IntPtr.Zero ? _hwnd : new WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero)
                return;

            if (Win32.GetForegroundWindow() != hwnd)
            {
                Activate();
                Win32.SetForegroundWindowAggressive(hwnd);
                if (Win32.GetForegroundWindow() != hwnd)
                {
                    ScheduleFocusRetry();
                    return;
                }
            }

            ItemsList.Focusable = true;
            _ = ItemsList.Focus();
            Keyboard.Focus(ItemsList);
            ItemsList.Focus();
        }
        catch { /* ignore */ }
        finally
        {
            swTotal.Stop();
            if (swTotal.ElapsedMilliseconds >= 25 && _perfFocusSlowLogCount < 20)
            {
                _perfFocusSlowLogCount++;
                ClipboardDiagnosticsLog.Write(
                    $"filejump.perf focus elapsedMs={swTotal.ElapsedMilliseconds} moveActive={_dockOwnerMoveActive}");
            }
        }
    }

    private void ScheduleFocusRetry()
    {
        if (_focusRetryCount >= 12) return;
        _focusRetryTimer?.Stop();
        _focusRetryTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(90) };
        _focusRetryTimer.Tick += (_, _) =>
        {
            _focusRetryTimer?.Stop();
            _focusRetryTimer = null;
            _focusRetryCount++;
            if (!IsLoaded) return;
            TryStealFocusForPicker();
        };
        _focusRetryTimer.Start();
    }

    private static bool IsPrimaryMouseButtonDown() =>
        (Win32.GetAsyncKeyState(0x01) & 0x8000) != 0;

    protected override void OnPreviewMouseDown(MouseButtonEventArgs e)
    {
        _clickReceivedByJumpPicker = true;
        base.OnPreviewMouseDown(e);
    }
}
