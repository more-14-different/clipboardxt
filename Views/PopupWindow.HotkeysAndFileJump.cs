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
    #region Keyboard Hook

    private static IntPtr StaticKeyboardHookProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        var owner = s_popupKeyboardHookOwner;
        var hhk = s_popupKeyboardHookForNext;
        if (owner != null && hhk != IntPtr.Zero)
        {
            try
            {
                return owner.KeyboardHookCallback(nCode, wParam, lParam);
            }
            catch (Exception ex)
            {
                ClipboardDiagnosticsLog.Write($"native keyboard hook exception: {ex}");
            }
        }
        return Win32.CallNextHookEx(hhk, nCode, wParam, lParam);
    }

    private void InstallKeyboardHook()
    {
        if (_keyboardHook != IntPtr.Zero) return;
        s_popupKeyboardHookOwner = this;
        ClipboardManager.Services.GlobalHookDispatcher.Dispatcher.Invoke(() =>
        {
            _keyboardHook = Win32.SetWindowsHookEx(
                Win32.WH_KEYBOARD_LL, s_popupKeyboardHookThunk, Win32.GetModuleHandle(null), 0);
            s_popupKeyboardHookForNext = _keyboardHook;
        });
    }

    private void UninstallKeyboardHook()
    {
        if (_keyboardHook != IntPtr.Zero)
        {
            var hk = _keyboardHook;
            _keyboardHook = IntPtr.Zero;
            ClipboardManager.Services.GlobalHookDispatcher.Dispatcher.Invoke(() => Win32.UnhookWindowsHookEx(hk));
        }
        if (s_popupKeyboardHookOwner == this)
        {
            s_popupKeyboardHookOwner = null;
            s_popupKeyboardHookForNext = IntPtr.Zero;
        }
    }

    /// <summary>对话框成为前台后分层短等再读路径：先快试，读不到再逐段补等（总上限接近原先单次长等）。</summary>
    private void ScheduleSnapshotFolderFromDialog(IntPtr dialogHwnd)
    {
        if (_appSettings == null || dialogHwnd == IntPtr.Zero) return;
        var nowSnap = Environment.TickCount64;
        if (dialogHwnd == _snapshotFolderDebounceHwnd
            && nowSnap - _snapshotFolderDebounceTick < 450)
            return;
        _snapshotFolderDebounceHwnd = dialogHwnd;
        _snapshotFolderDebounceTick = nowSnap;

        unchecked { _dialogSnapshotScheduleGen++; }
        var scheduleGen = _dialogSnapshotScheduleGen;
        var target = dialogHwnd;
        Dispatcher.BeginInvoke(() =>
        {
            void SchedulePhase(int phase)
            {
                var delayMs = phase switch { 0 => 80, 1 => 120, 2 => 120, _ => 0 };
                if (delayMs == 0) return;
                var timer = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(delayMs)
                };
                var p = phase;
                timer.Tick += (_, _) =>
                {
                    timer.Stop();
                    if (scheduleGen != _dialogSnapshotScheduleGen) return;
                    if (_appSettings == null) return;
                    if (!Win32.IsWindow(target)) return;
                    var fgSnap = Win32.GetForegroundWindow();
                    if (FileDialogJumpHelper.ResolveFileDialogHwndFromWindowOrAncestor(fgSnap) != target) return;
                    // DLL 注入读取路径较慢，放到后台线程避免阻塞 UI
                    var capturedTarget = target;
                    var capturedPhase = p;
                    var th = new Thread(() =>
                    {
                        if (FileDialogJumpHelper.TryReadCurrentFolder(capturedTarget, out var folder)
                            && !string.IsNullOrEmpty(folder))
                        {
                            Dispatcher.BeginInvoke(() => RememberLastDialogFolder(folder), DispatcherPriority.Background);
                            return;
                        }
                        if (capturedPhase < 2)
                            Dispatcher.BeginInvoke(() => SchedulePhase(capturedPhase + 1), DispatcherPriority.Background);
                    }) { IsBackground = true, Name = "ClipboardX-SnapshotRead" };
                    th.Start();
                };
                timer.Start();
            }

            SchedulePhase(0);
        }, System.Windows.Threading.DispatcherPriority.Background);
    }

    /// <summary>前台刚从「打开/保存」对话框切走时，尝试记下当时所在文件夹。</summary>
    private void TryRememberFolderFromDialog(IntPtr previousHwnd)
    {
        if (_appSettings == null || previousHwnd == IntPtr.Zero || previousHwnd == _hwnd) return;
        if (!Win32.IsWindow(previousHwnd)) return;

        var startWorker = false;
        lock (_externalFolderResolveGate)
        {
            if (_externalFolderResolveStopping) return;
            _pendingDialogFolderResolvePrevious = previousHwnd;
            if (!_externalFolderResolveWorkerRunning)
            {
                _externalFolderResolveWorkerRunning = true;
                startWorker = true;
            }
        }

        if (startWorker)
            StartExternalFolderResolveWorker();
    }

    private void RememberLastDialogFolder(string folder)
    {
        if (_appSettings == null) return;
        _appSettings.RecordRecentFolderUse(folder);
    }

    private static List<string>? CopyRecentForJump(AppSettings? settings)
    {
        if (settings?.RecentFileDialogFolders == null || settings.RecentFileDialogFolders.Count == 0)
            return null;
        var maxCount = Math.Clamp(settings.RecentFolderMaxCount, 1, 50);
        var list = new List<string>();
        foreach (var p in settings.RecentFileDialogFolders)
        {
            if (string.IsNullOrWhiteSpace(p)) continue;
            list.Add(p.Trim());
            if (list.Count >= maxCount) break;
        }

        return list.Count > 0 ? list : null;
    }

    /// <summary>
    /// 记录最近一次活跃外部文件管理器的路径；切回文件对话框时优先将其作为同步目标。
    /// </summary>
    private void ScheduleRememberExternalManagerFolders(IntPtr previousHwnd, IntPtr currentHwnd)
    {
        var startWorker = false;
        lock (_externalFolderResolveGate)
        {
            if (_externalFolderResolveStopping) return;
            _pendingExternalFolderResolvePrevious = previousHwnd;
            _pendingExternalFolderResolveCurrent = currentHwnd;
            if (!_externalFolderResolveWorkerRunning)
            {
                _externalFolderResolveWorkerRunning = true;
                startWorker = true;
            }
        }

        if (startWorker)
            StartExternalFolderResolveWorker();
    }

    private void StartExternalFolderResolveWorker()
    {
        var worker = new Thread(ExternalManagerFolderResolveLoop)
        {
            IsBackground = true,
            Name = "ClipboardX-ExternalFolder-Resolve",
            Priority = ThreadPriority.BelowNormal,
        };
        worker.SetApartmentState(ApartmentState.STA);
        worker.Start();
    }

    private void ExternalManagerFolderResolveLoop()
    {
        while (true)
        {
            IntPtr dialogPreviousHwnd;
            IntPtr previousHwnd;
            IntPtr currentHwnd;
            lock (_externalFolderResolveGate)
            {
                if (_externalFolderResolveStopping)
                {
                    _externalFolderResolveWorkerRunning = false;
                    return;
                }

                dialogPreviousHwnd = _pendingDialogFolderResolvePrevious;
                previousHwnd = _pendingExternalFolderResolvePrevious;
                currentHwnd = _pendingExternalFolderResolveCurrent;
                _pendingDialogFolderResolvePrevious = IntPtr.Zero;
                _pendingExternalFolderResolvePrevious = IntPtr.Zero;
                _pendingExternalFolderResolveCurrent = IntPtr.Zero;
                if (dialogPreviousHwnd == IntPtr.Zero
                    && previousHwnd == IntPtr.Zero
                    && currentHwnd == IntPtr.Zero)
                {
                    _externalFolderResolveWorkerRunning = false;
                    return;
                }
            }

            ResolveDialogFolderAndPost(dialogPreviousHwnd);
            ResolveExternalManagerFolderAndPost(previousHwnd);
            if (currentHwnd != previousHwnd)
                ResolveExternalManagerFolderAndPost(currentHwnd);
        }
    }

    private void ResolveDialogFolderAndPost(IntPtr previousHwnd)
    {
        if (previousHwnd == IntPtr.Zero || previousHwnd == _hwnd || !Win32.IsWindow(previousHwnd))
            return;
        var dialog = FileDialogJumpHelper.ResolveFileDialogHwndFromWindowOrAncestor(previousHwnd);
        if (dialog == IntPtr.Zero) return;
        if (!FileDialogJumpHelper.TryReadCurrentFolder(dialog, out var folder)
            || string.IsNullOrEmpty(folder))
            return;
        try
        {
            Dispatcher.BeginInvoke(
                DispatcherPriority.Background,
                (Action)(() => RememberLastDialogFolder(folder)));
        }
        catch
        {
            /* Dispatcher 正在退出 */
        }
    }

    private void ResolveExternalManagerFolderAndPost(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero || hwnd == _hwnd || !Win32.IsWindow(hwnd)) return;
        if (FileDialogJumpHelper.IsLikelyFileDialog(hwnd)) return;

        // 跳过需要通过剪贴板通信的文件管理器（XYplorer、Total Commander），
        // 避免每次前台切换都发送脚本导致 scripting console 异常和剪贴板被覆盖。
        // 这些管理器的路径仅在文件对话框打开时按需采集（CollectCandidates）。
        var rootCls = Win32.GetWindowClassName(Win32.GetAncestor(hwnd, Win32.GA_ROOT));
        if (rootCls is "ThunderRT6FormDC" or "TTOTAL_CMD") return;

        var folder = FileManagerPathCollector.TryGetFolderForWindow(hwnd);
        if (string.IsNullOrWhiteSpace(folder)) return;

        var root = Win32.GetAncestor(hwnd, Win32.GA_ROOT);
        if (root == IntPtr.Zero) root = hwnd;
        var normalized = folder.Trim();
        try
        {
            Dispatcher.BeginInvoke(
                DispatcherPriority.Background,
                (Action)(() => ApplyExternalManagerFolderResolution(root, normalized)));
        }
        catch
        {
            /* Dispatcher 正在退出 */
        }
    }

    private void ApplyExternalManagerFolderResolution(IntPtr root, string normalized)
    {
        if (_externalFolderResolveStopping) return;
        if (string.Equals(_lastExternalFolder, normalized, StringComparison.OrdinalIgnoreCase)
            && _lastExternalManagerRoot == root)
            return;

        _lastExternalFolder = normalized;
        _lastExternalManagerRoot = root;
        ShellNavigateLog.Write("filejump", $"external folder updated root=0x{root.ToInt64():X} path=\"{normalized}\"");
    }

    #endregion
}
