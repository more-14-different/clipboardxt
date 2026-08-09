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
    private void TryJumpFileDialogToLastFolder()
    {
        if (_appSettings == null) return;

        try
        {
            TryJumpFileDialogToLastFolderCore();
        }
        catch (Exception ex)
        {
            ShellNavigateLog.Write("filejump", "TryJumpFileDialogToLastFolder: " + ex);
        }
    }

    private void TryJumpFileDialogToLastFolderCore()
    {
        if (_appSettings == null) return;

        var fgNow = Win32.GetForegroundWindow();
        var dialogHwnd = ResolveFileJumpTargetHwndInternal(fgNow);
        if (dialogHwnd == IntPtr.Zero)
        {
            OpenGlobalFavoritesPicker(fgNow);
            return;
        }

        // 列表窗正在 new 的过程中尚未赋给 _activeFileJumpPicker：忽略重复热键，避免再次排队打开。
        if (_fileJumpPickerOpenInProgress && _activeFileJumpPicker == null)
            return;

        var mem = _appSettings.LastFileDialogFolder?.Trim();
        var allowShellInject = _appSettings.EnableShellNavigateInject;

        unchecked { _fileJumpHotkeyCollectGen++; }
        var gen = _fileJumpHotkeyCollectGen;
        var dialogHwndCapture = dialogHwnd;
        var memCapture = mem;
        var recentCapture = CopyRecentForJump(_appSettings);
        var allowCapture = allowShellInject;

        void StaCollect()
        {
            if (gen != _fileJumpHotkeyCollectGen) return;
            List<FileJumpCandidate> quick;
            try
            {
                quick = FileManagerPathCollector.CollectCandidates(dialogHwndCapture, memCapture,
                    skipAlternateUiAutomation: true, stopAfterCandidateCount: 2,
                    shouldAbort: () => gen != _fileJumpHotkeyCollectGen,
                    recentFolders: recentCapture);
            }
            catch (Exception ex)
            {
                ShellNavigateLog.Write("filejump", "CollectCandidates quick (hotkey): " + ex);
                quick = new List<FileJumpCandidate>();
            }

            if (gen != _fileJumpHotkeyCollectGen) return;

            if (quick.Count >= 2)
            {
                Dispatcher.BeginInvoke(() =>
                {
                    if (gen != _fileJumpHotkeyCollectGen) return;
                    TryJumpFileDialogToLastFolderContinueAfterCollect(
                        dialogHwndCapture,
                        quick,
                        allowCapture,
                        afterPickerAssigned: () => StartFullCollectForHotkey(dialogHwndCapture, memCapture, recentCapture, gen));
                }, DispatcherPriority.Input);
                return;
            }

            if (gen != _fileJumpHotkeyCollectGen) return;

            List<FileJumpCandidate> candidates;
            try
            {
                candidates = FileManagerPathCollector.CollectCandidates(dialogHwndCapture, memCapture,
                    shouldAbort: () => gen != _fileJumpHotkeyCollectGen,
                    recentFolders: recentCapture);
            }
            catch (Exception ex)
            {
                ShellNavigateLog.Write("filejump", "CollectCandidates (hotkey): " + ex);
                candidates = new List<FileJumpCandidate>();
            }

            Dispatcher.BeginInvoke(() =>
            {
                if (gen != _fileJumpHotkeyCollectGen) return;
                TryJumpFileDialogToLastFolderContinueAfterCollect(dialogHwndCapture, candidates, allowCapture);
            }, DispatcherPriority.Normal);
        }

        var th = new Thread(StaCollect)
        {
            IsBackground = true,
            Name = "ClipboardX-FileJump-Hotkey-Collect",
        };
        th.SetApartmentState(ApartmentState.STA);
        th.Start();
    }

    /// <summary>全局 Ctrl+G：非文件对话框界面打开收藏/常用文件夹快速选择窗口。</summary>
    private void OpenGlobalFavoritesPicker(IntPtr standaloneTargetHwnd)
    {
        if (_appSettings == null) return;
        if (_activeFileJumpPicker != null || _fileJumpPickerOpenInProgress) return;

        var candidates = new List<FileJumpCandidate>();

        foreach (var fav in _appSettings.FolderFavorites)
        {
            if (string.IsNullOrWhiteSpace(fav.Path)) continue;
            try
            {
                var full = Path.GetFullPath(fav.Path.Trim());
                candidates.Add(new FileJumpCandidate("收藏", full));
            }
            catch { /* ignore */ }
        }

        foreach (var r in _appSettings.RecentFileDialogFolders)
        {
            if (string.IsNullOrWhiteSpace(r)) continue;
            try
            {
                var full = Path.GetFullPath(r.Trim());
                if (!candidates.Any(c =>
                    string.Equals(c.Path, full, StringComparison.OrdinalIgnoreCase)))
                    candidates.Add(new FileJumpCandidate("常用", full));
            }
            catch { /* ignore */ }
        }

        Win32.GetCursorPos(out var pos);
        var picker = new FileDialogJumpPickerWindow(
            candidates, 0, pos.X, pos.Y, _appSettings, IntPtr.Zero,
            autoForegroundStickyMode: false,
            standaloneTargetHwnd: standaloneTargetHwnd,
            standalonePasteCallback: FileJumpStandalonePasteAsync);
        _activeFileJumpPicker = picker;
        picker.Closed += (_, _) =>
        {
            if (ReferenceEquals(_activeFileJumpPicker, picker))
                _activeFileJumpPicker = null;
        };
        picker.Show();
    }

    private void TryJumpFileDialogToLastFolderContinueAfterCollect(
        IntPtr dialogHwnd,
        List<FileJumpCandidate> candidates,
        bool allowShellInject,
        Action? afterPickerAssigned = null)
    {
        if (_appSettings == null) return;
        if (dialogHwnd == IntPtr.Zero || !Win32.IsWindow(dialogHwnd))
        {
            ClearFileJumpDoubleTapState();
            return;
        }

        if (candidates.Count == 0)
        {
            ClearFileJumpDoubleTapState();
            return;
        }

        var prefer = PreferCandidateIndex(dialogHwnd, candidates);

        if (_activeFileJumpPicker != null)
        {
            _fileJumpPickerSession++;
            ClearFileJumpDoubleTapState();
            NavigateToFolderInBackground(dialogHwnd, candidates[prefer].Path, allowShellInject);
            Dispatcher.BeginInvoke(() => _activeFileJumpPicker?.Close(),
                System.Windows.Threading.DispatcherPriority.Normal);
            return;
        }

        var tick = Environment.TickCount64;
        var sameDialog = dialogHwnd == _fileJumpLastDialogHwnd;
        var withinDoubleTap = sameDialog && _fileJumpLastHotkeyTick != 0
                                        && tick - _fileJumpLastHotkeyTick >= 0
                                        && tick - _fileJumpLastHotkeyTick <= FileJumpDoubleTapMs;

        if (withinDoubleTap)
        {
            _fileJumpPickerSession++;
            ClearFileJumpDoubleTapState();
            var path = candidates[prefer].Path;
            NavigateToFolderInBackground(dialogHwnd, path, allowShellInject);
            Dispatcher.BeginInvoke(() => _activeFileJumpPicker?.Close(),
                System.Windows.Threading.DispatcherPriority.Normal);
            return;
        }

        if (!_appSettings.FileJumpPickerAutoPopup)
        {
            ClearFileJumpDoubleTapState();
            NavigateToFolderInBackground(dialogHwnd, candidates[prefer].Path, allowShellInject);
            return;
        }

        ScheduleFileJumpPickerOpen(dialogHwnd, candidates.ToList(), prefer, armHotkeyDoubleTap: true, allowShellInject,
            autoForegroundStickyMode: false, afterPickerAssigned);
    }

    /// <summary>在后台 STA 线程执行文件对话框导航，避免 Thread.Sleep 阻塞 UI 线程。</summary>
    private void NavigateToFolderInBackground(IntPtr dialogHwnd, string path, bool allowShellInject,
        Action<bool>? onCompleted = null)
    {
        MarkFileJumpNavigationSuppressed(dialogHwnd, path);
        var th = new Thread(() =>
        {
            try
            {
                var ok = FileDialogJumpHelper.TryNavigateToFolder(dialogHwnd, path, allowShellInject);
                if (ok)
                {
                    Dispatcher.BeginInvoke(() => _appSettings?.RecordRecentFolderUse(path),
                        System.Windows.Threading.DispatcherPriority.Background);
                }
                onCompleted?.Invoke(ok);
            }
            catch (Exception ex)
            {
                ShellNavigateLog.Write("filejump", "NavigateToFolderInBackground: " + ex);
                onCompleted?.Invoke(false);
            }
        })
        {
            IsBackground = true,
            Name = "ClipboardX-FileJump-Navigate",
        };
        th.SetApartmentState(ApartmentState.STA);
        th.Start();
    }

    /// <summary>
    /// picker 打开时切换到外部文件管理器，触发一次采集以刷新 picker 列表（将新 Explorer 路径加入候选）。
    /// </summary>
}
