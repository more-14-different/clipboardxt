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
    private void TryAutoOpenFileJumpPickerWhenDialogForeground(IntPtr foregroundHwnd)
    {
        if (_appSettings == null || !_appSettings.FileJumpPickerOpenWhenDialogForeground) return;
        if (foregroundHwnd == IntPtr.Zero) return;

        _fileJumpAutoOpenDebounceTimer?.Stop();
        _fileJumpAutoOpenDebounceTimer = null;
        var hwndCapture = foregroundHwnd;
        var delayMs = Math.Clamp(_appSettings.FileJumpPickerShowDelayMs, 0, 10000);
        // 打开/保存窗到前台时，系统常在极短时间内多次触发；延时为 0 时仍用约一帧的防抖合并，避免并行多个 STA 采集线程。
        // 原先固定 80ms 会明显拖慢「立即弹出」的体感。
        var effectiveMs = delayMs <= 0 ? 16 : delayMs;

        _fileJumpAutoOpenDebounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(effectiveMs) };
        _fileJumpAutoOpenDebounceTimer.Tick += (_, _) =>
        {
            _fileJumpAutoOpenDebounceTimer?.Stop();
            _fileJumpAutoOpenDebounceTimer = null;
            try
            {
                TryAutoOpenFileJumpPickerWhenDialogForegroundAfterDebounce(hwndCapture);
            }
            catch (Exception ex)
            {
                ShellNavigateLog.Write("filejump", "TryAutoOpenFileJumpPickerWhenDialogForegroundAfterDebounce: " + ex);
            }
        };
        _fileJumpAutoOpenDebounceTimer.Start();
    }

    private void ScheduleAutoOpenFileJumpPickerRetry(IntPtr dialogHwnd, IntPtr dialogRoot)
    {
        if (dialogHwnd == IntPtr.Zero || dialogRoot == IntPtr.Zero) return;
        if (_fileJumpAutoOpenRetryRoot != dialogRoot)
        {
            _fileJumpAutoOpenRetryRoot = dialogRoot;
            _fileJumpAutoOpenRetryCount = 0;
        }
        if (_fileJumpAutoOpenRetryCount >= 2) return;

        _fileJumpAutoOpenRetryCount++;
        _fileJumpAutoOpenDebounceTimer?.Stop();
        _fileJumpAutoOpenDebounceTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(_fileJumpAutoOpenRetryCount == 1 ? 160 : 260)
        };
        var capturedDialog = dialogHwnd;
        _fileJumpAutoOpenDebounceTimer.Tick += (_, _) =>
        {
            _fileJumpAutoOpenDebounceTimer?.Stop();
            _fileJumpAutoOpenDebounceTimer = null;
            TryAutoOpenFileJumpPickerWhenDialogForegroundAfterDebounce(capturedDialog);
        };
        _fileJumpAutoOpenDebounceTimer.Start();
    }

    /// <summary>
    /// 对话框成为前台并经过短延时后：按设置自动弹出跳转列表或直跳最优路径（与 FileJumpPickerAutoPopup 一致，含仅 1 条候选）。
    /// </summary>
    private void TryAutoOpenFileJumpPickerWhenDialogForegroundAfterDebounce(IntPtr foregroundHwnd)
    {
        if (_appSettings == null || !_appSettings.FileJumpPickerOpenWhenDialogForeground) return;
        if (foregroundHwnd == IntPtr.Zero) return;
        if (_fileJumpPickerOpenInProgress && _activeFileJumpPicker == null) return;
        if (_activeFileJumpPicker != null) return;

        if (_fileJumpAutoOpenPickerDoneRoot != IntPtr.Zero
            && !Win32.IsWindow(_fileJumpAutoOpenPickerDoneRoot))
            _fileJumpAutoOpenPickerDoneRoot = IntPtr.Zero;

        var fgNow = Win32.GetForegroundWindow();
        var dialogHwnd = ResolveFileJumpTargetHwndInternal(fgNow);
        if (dialogHwnd == IntPtr.Zero) return;
        if (IsFileJumpNavigationSuppressed(dialogHwnd)) return;

        var dialogRoot = Win32.GetAncestor(dialogHwnd, Win32.GA_ROOT);
        if (dialogRoot == IntPtr.Zero) return;

        if (!IsForegroundFocusOnFileDialogRoot(dialogRoot))
            return;

        if (_fileJumpAutoOpenRetryRoot != dialogRoot)
        {
            _fileJumpAutoOpenRetryRoot = dialogRoot;
            _fileJumpAutoOpenRetryCount = 0;
        }

        if (dialogRoot == _fileJumpAutoOpenPickerDoneRoot)
            return;

        var mem = _appSettings.LastFileDialogFolder?.Trim();
        var allowShellInject = _appSettings.EnableShellNavigateInject;

        unchecked { _fileJumpAutoForegroundCollectGen++; }
        var gen = _fileJumpAutoForegroundCollectGen;
        var dialogHwndCapture = dialogHwnd;
        var dialogRootCapture = dialogRoot;
        var memCapture = mem;
        var recentCapture = CopyRecentForJump(_appSettings);
        var allowCapture = allowShellInject;

        void StaCollect()
        {
            if (gen != _fileJumpAutoForegroundCollectGen) return;
            List<FileJumpCandidate> quick;
            try
            {
                quick = FileManagerPathCollector.CollectCandidates(dialogHwndCapture, memCapture,
                    skipAlternateUiAutomation: true, stopAfterCandidateCount: 2,
                    shouldAbort: () => gen != _fileJumpAutoForegroundCollectGen,
                    recentFolders: recentCapture);
            }
            catch (Exception ex)
            {
                ShellNavigateLog.Write("filejump", "CollectCandidates quick (auto fg): " + ex);
                quick = new List<FileJumpCandidate>();
            }

            if (gen != _fileJumpAutoForegroundCollectGen) return;

            if (quick.Count >= 2)
            {
                Dispatcher.BeginInvoke(() =>
                {
                    if (gen != _fileJumpAutoForegroundCollectGen) return;
                    TryAutoOpenFileJumpPickerAfterCollect(
                        dialogHwndCapture,
                        dialogRootCapture,
                        quick,
                        allowCapture,
                        afterPickerAssigned: () =>
                            StartFullCollectForAutoForeground(dialogHwndCapture, memCapture, recentCapture, gen));
                }, DispatcherPriority.Input);
                return;
            }

            if (gen != _fileJumpAutoForegroundCollectGen) return;

            List<FileJumpCandidate> candidates;
            try
            {
                candidates = FileManagerPathCollector.CollectCandidates(dialogHwndCapture, memCapture,
                    shouldAbort: () => gen != _fileJumpAutoForegroundCollectGen,
                    recentFolders: recentCapture);
            }
            catch (Exception ex)
            {
                ShellNavigateLog.Write("filejump", "CollectCandidates (auto fg): " + ex);
                candidates = new List<FileJumpCandidate>();
            }

            Dispatcher.BeginInvoke(() =>
            {
                if (gen != _fileJumpAutoForegroundCollectGen) return;
                TryAutoOpenFileJumpPickerAfterCollect(dialogHwndCapture, dialogRootCapture, candidates, allowCapture);
            }, DispatcherPriority.Normal);
        }

        var th = new Thread(StaCollect)
        {
            IsBackground = true,
            Name = "ClipboardX-FileJump-AutoFg-Collect",
        };
        th.SetApartmentState(ApartmentState.STA);
        th.Start();
    }

    private void TryAutoOpenFileJumpPickerAfterCollect(
        IntPtr dialogHwnd,
        IntPtr dialogRoot,
        List<FileJumpCandidate> candidates,
        bool allowShellInject,
        Action? afterPickerAssigned = null)
    {
        if (_appSettings == null || !_appSettings.FileJumpPickerOpenWhenDialogForeground) return;
        if (dialogHwnd == IntPtr.Zero || !Win32.IsWindow(dialogHwnd)) return;

        var dialogRootNow = Win32.GetAncestor(dialogHwnd, Win32.GA_ROOT);
        if (dialogRootNow == IntPtr.Zero || dialogRootNow != dialogRoot) return;

        // 采集在线程里异步完成；列表窗可能已通过 BeginInvoke 打开：避免再次直跳/调度造成 WPS Qt 路径重复导航
        if (_activeFileJumpPicker != null) return;
        if (_fileJumpPickerOpenInProgress) return;

        if (!IsForegroundFocusOnFileDialogRoot(dialogRootNow)) return;
        if (IsFileJumpNavigationSuppressed(dialogHwnd)) return;

        if (dialogRootNow == _fileJumpAutoOpenPickerDoneRoot) return;

        if (candidates.Count == 0)
        {
            ScheduleAutoOpenFileJumpPickerRetry(dialogHwnd, dialogRootNow);
            return;
        }

        _fileJumpAutoOpenRetryRoot = IntPtr.Zero;
        _fileJumpAutoOpenRetryCount = 0;
        var prefer = PreferCandidateIndex(dialogHwnd, candidates);
        _fileJumpAutoOpenPickerDoneRoot = dialogRootNow;

        // A 方案：「自动跳转到最佳路径」与「自动弹出列表」可叠加 ——
        // 弹出列表的同时立刻直跳首条，用户能在列表里再换。
        var autoNavigate = _appSettings.FileJumpAutoOnFirstClick;
        if (autoNavigate)
        {
            NavigateToFolderInBackground(dialogHwnd, candidates[prefer].Path, allowShellInject);
            _fileJumpAutoFirstJumpDoneRoot = dialogRootNow;
            DisarmFileJumpClickToNavigate();
        }

        ScheduleFileJumpPickerOpen(dialogHwnd, candidates.ToList(), prefer, armHotkeyDoubleTap: false, allowShellInject,
            autoForegroundStickyMode: true, afterPickerAssigned);
    }

}
