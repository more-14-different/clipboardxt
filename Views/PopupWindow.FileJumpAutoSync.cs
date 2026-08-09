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
    #region 切回对话框自动同步路径

    /// <summary>
    /// 对话框再次到前台时，重新采集候选路径；
    /// 若能确定最近一次外部文件管理器路径，且与对话框当前路径不同，则自动同步过去。
    /// </summary>
    /// <param name="previousForegroundHwnd">本次获得前台之前的窗口；仅当其为可解析目录的外部管理器时才用 <see cref="_lastExternalFolder"/> 驱动跳转，避免用户在对话框内改路径后到其它程序再切回被误拉到资源管理器旧目录。</param>
    private void TryAutoSyncPathOnDialogReturn(IntPtr foregroundHwnd, IntPtr previousForegroundHwnd)
    {
        if (_appSettings == null) return;
        if (foregroundHwnd == IntPtr.Zero) return;

        var dialogHwnd = ResolveFileJumpTargetHwndInternal(foregroundHwnd);
        if (dialogHwnd == IntPtr.Zero) return;
        var dialogRoot = Win32.GetAncestor(dialogHwnd, Win32.GA_ROOT);
        if (dialogRoot == IntPtr.Zero) return;
        if (IsFileJumpNavigationSuppressed(dialogHwnd)) return;

        var hasMatchingPicker = ActivePickerMatchesDialog(dialogRoot);
        if (!hasMatchingPicker && !_appSettings.FileJumpAutoSyncOnReturn) return;

        unchecked { _fileJumpAutoSyncScheduleGen++; }
        var scheduleGen = _fileJumpAutoSyncScheduleGen;
        var hwndCapture = dialogHwnd;
        var rootCapture = dialogRoot;
        var prevCapture = previousForegroundHwnd;

        void SchedulePrecheck(int phase)
        {
            var delayMs = phase switch { 0 => 50, 1 => 100, 2 => 100, _ => 0 };
            if (delayMs == 0) return;
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(delayMs) };
            var p = phase;
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                if (scheduleGen != _fileJumpAutoSyncScheduleGen) return;
                if (!TryAutoSyncForegroundStable(hwndCapture, rootCapture))
                {
                    if (p < 2)
                        SchedulePrecheck(p + 1);
                    return;
                }

                try
                {
                    TryAutoSyncPathOnDialogReturnCore(hwndCapture, rootCapture, prevCapture);
                }
                catch (Exception ex)
                {
                    ShellNavigateLog.Write("filejump", "TryAutoSyncPathOnDialogReturnCore: " + ex);
                }
            };
            timer.Start();
        }

        SchedulePrecheck(0);
    }

    /// <summary>仅当前台刚从「能采到文件夹路径、且不是文件对话框」的窗口切来时，才信任 <see cref="_lastExternalFolder"/> 作为自动同步目标。</summary>
    private bool ShouldPreferLastExternalFolderForAutoSync(IntPtr previousForegroundHwnd)
    {
        if (previousForegroundHwnd == IntPtr.Zero || previousForegroundHwnd == _hwnd || !Win32.IsWindow(previousForegroundHwnd))
            return false;
        if (FileDialogJumpHelper.IsLikelyFileDialog(previousForegroundHwnd)) return false;
        var folder = FileManagerPathCollector.TryGetFolderForWindow(previousForegroundHwnd);
        return !string.IsNullOrWhiteSpace(folder);
    }

    /// <summary>切回同步前：前台根窗口已与目标对话框一致（分层短等后再判）。</summary>
    private bool TryAutoSyncForegroundStable(IntPtr dialogHwnd, IntPtr dialogRoot)
    {
        if (_appSettings == null) return false;
        if (!Win32.IsWindow(dialogHwnd)) return false;
        return IsForegroundFocusOnFileDialogRoot(dialogRoot);
    }

    private void TryAutoSyncPathOnDialogReturnCore(IntPtr dialogHwnd, IntPtr dialogRoot, IntPtr previousForegroundHwnd)
    {
        if (_appSettings == null) return;
        if (!Win32.IsWindow(dialogHwnd)) return;

        if (!IsForegroundFocusOnFileDialogRoot(dialogRoot)) return;

        var allowShellInject = _appSettings.EnableShellNavigateInject;
        var preferLastExternal = ShouldPreferLastExternalFolderForAutoSync(previousForegroundHwnd);

        // 直接读取前一个窗口的最新路径，而非使用可能过时的 _lastExternalFolder
        var preferredExternalFolder = "";
        if (preferLastExternal && previousForegroundHwnd != IntPtr.Zero)
        {
            var directPath = FileManagerPathCollector.TryGetFolderForWindow(previousForegroundHwnd, fresh: true);
            if (!string.IsNullOrEmpty(directPath) && Directory.Exists(directPath))
                preferredExternalFolder = directPath.Trim();
            else if (!string.IsNullOrEmpty(_lastExternalFolder))
                preferredExternalFolder = _lastExternalFolder.Trim();
        }

        var mem = !string.IsNullOrEmpty(preferredExternalFolder)
            ? preferredExternalFolder
            : _appSettings.LastFileDialogFolder?.Trim();

        var recentCapture = CopyRecentForJump(_appSettings);

        unchecked { _fileJumpAutoSyncCollectGen++; }
        var gen = _fileJumpAutoSyncCollectGen;
        var dialogCapture = dialogHwnd;
        var dialogRootCapture = dialogRoot;

        void StaCollect()
        {
            List<FileJumpCandidate> candidates;
            try
            {
                candidates = FileManagerPathCollector.CollectCandidates(dialogCapture, mem,
                    recentFolders: recentCapture);
            }
            catch (Exception ex)
            {
                ShellNavigateLog.Write("filejump", "CollectCandidates (auto-sync): " + ex);
                return;
            }

            Dispatcher.BeginInvoke(() =>
            {
                if (gen != _fileJumpAutoSyncCollectGen) return;
                if (candidates.Count == 0) return;

                var preferredPath = ResolvePreferredExternalFolder(candidates, preferredExternalFolder);

                RefreshActiveFileJumpPicker(dialogCapture, dialogRootCapture, candidates, preferredPath);

                if (!_appSettings.FileJumpAutoSyncOnReturn) return;
                if (string.IsNullOrEmpty(preferredPath)) return;

                // DLL 注入读取路径较慢，放到后台线程避免阻塞 UI
                var capturedDialog = dialogCapture;
                var capturedRoot = dialogRootCapture;
                var capturedPreferred = preferredPath;
                var capturedAllowInject = allowShellInject;
                var thRead = new Thread(() =>
                {
                    string? currentFolder = null;
                    if (FileDialogJumpHelper.TryReadCurrentFolder(capturedDialog, out var currentFolderRead)
                        && !string.IsNullOrEmpty(currentFolderRead))
                    {
                        currentFolder = currentFolderRead;
                        var norm1 = NormalizeFolderPathForCompare(currentFolderRead);
                        var norm2 = NormalizeFolderPathForCompare(capturedPreferred);
                        if (string.Equals(norm1, norm2, StringComparison.OrdinalIgnoreCase))
                            return;
                    }

                    Dispatcher.BeginInvoke(() =>
                    {
                        if (IsFileJumpNavigationSuppressed(capturedDialog, capturedPreferred))
                            return;
                        if (TryNavigateViaActivePicker(capturedDialog, capturedRoot, capturedPreferred))
                            return;
                        ShellNavigateLog.Write("filejump",
                            $"auto-sync navigating from \"{currentFolder ?? "(unreadable)"}\" to \"{capturedPreferred}\"");
                        NavigateToFolderInBackground(capturedDialog, capturedPreferred, capturedAllowInject);
                    }, DispatcherPriority.Normal);
                }) { IsBackground = true, Name = "ClipboardX-AutoSyncRead" };
                thRead.Start();
            }, DispatcherPriority.Normal);
        }

        var th = new Thread(StaCollect)
        {
            IsBackground = true,
            Name = "ClipboardX-FileJump-AutoSync-Collect",
        };
        th.SetApartmentState(ApartmentState.STA);
        th.Start();
    }

    private bool ActivePickerMatchesDialog(IntPtr dialogRoot)
    {
        if (_activeFileJumpPicker == null) return false;
        var pickerDialog = _activeFileJumpPicker.OwnerDialogHwnd;
        if (pickerDialog == IntPtr.Zero || !Win32.IsWindow(pickerDialog)) return false;
        var pickerRoot = Win32.GetAncestor(pickerDialog, Win32.GA_ROOT);
        return pickerRoot != IntPtr.Zero && pickerRoot == dialogRoot;
    }

    private void RefreshActiveFileJumpPicker(
        IntPtr dialogHwnd,
        IntPtr dialogRoot,
        List<FileJumpCandidate> candidates,
        string? preferredPath)
    {
        if (_activeFileJumpPicker == null) return;
        if (!ActivePickerMatchesDialog(dialogRoot)) return;
        _activeFileJumpPicker.RefreshCandidatesFromExternal(candidates, preferredPath);
    }

    private bool TryNavigateViaActivePicker(IntPtr dialogHwnd, IntPtr dialogRoot, string preferredPath)
    {
        if (_activeFileJumpPicker == null) return false;
        if (!ActivePickerMatchesDialog(dialogRoot)) return false;
        if (!_activeFileJumpPicker.IsAutoForegroundStickyMode) return false;
        _activeFileJumpPicker.NavigateKeepOpenToPath(preferredPath);
        return true;
    }

    private static string? ResolvePreferredExternalFolder(
        IReadOnlyList<FileJumpCandidate> candidates,
        string preferredExternalFolder)
    {
        if (!string.IsNullOrWhiteSpace(preferredExternalFolder))
        {
            var matched = candidates.FirstOrDefault(c =>
                string.Equals(
                    NormalizeFolderPathForCompare(c.Path),
                    NormalizeFolderPathForCompare(preferredExternalFolder),
                    StringComparison.OrdinalIgnoreCase));
            if (matched != null && !string.IsNullOrEmpty(matched.Path))
                return matched.Path;

            try
            {
                if (Directory.Exists(preferredExternalFolder))
                    return Path.GetFullPath(preferredExternalFolder);
            }
            catch
            {
                // ignore
            }
        }

        return null;
    }

    #endregion
}
