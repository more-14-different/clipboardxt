using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using ClipboardManager.Models;

namespace ClipboardManager;

public partial class PopupWindow : Window
{
    /// <summary>完整采集（含 UIA）完成后刷新已打开的跳转列表。</summary>
    private void StartFullCollectForHotkey(IntPtr dialogHwnd, string? mem, List<string>? recentFolders, int gen)
    {
        Task.Run(() =>
        {
            if (!WaitForFileJumpFullCollectQuietWindow(dialogHwnd, () => gen != _fileJumpHotkeyCollectGen))
                return;

            List<FileJumpCandidate> full;
            try
            {
                full = FileManagerPathCollector.CollectCandidates(dialogHwnd, mem,
                    shouldAbort: () => gen != _fileJumpHotkeyCollectGen,
                    recentFolders: recentFolders);
            }
            catch (Exception ex)
            {
                ShellNavigateLog.Write("filejump", "CollectCandidates (hotkey full): " + ex);
                full = new List<FileJumpCandidate>();
            }

            Dispatcher.BeginInvoke(() =>
            {
                if (gen != _fileJumpHotkeyCollectGen) return;
                TryJumpFileDialogRefreshPickerIfOpen(dialogHwnd, full);
            }, DispatcherPriority.Normal);
        });
    }

    /// <summary>完整采集（含 UIA）完成后刷新已打开的跳转列表。</summary>
    private void StartFullCollectForAutoForeground(IntPtr dialogHwnd, string? mem, List<string>? recentFolders,
        int gen)
    {
        Task.Run(() =>
        {
            if (!WaitForFileJumpFullCollectQuietWindow(dialogHwnd, () => gen != _fileJumpAutoForegroundCollectGen))
                return;

            List<FileJumpCandidate> full;
            try
            {
                full = FileManagerPathCollector.CollectCandidates(dialogHwnd, mem,
                    shouldAbort: () => gen != _fileJumpAutoForegroundCollectGen,
                    recentFolders: recentFolders);
            }
            catch (Exception ex)
            {
                ShellNavigateLog.Write("filejump", "CollectCandidates (auto fg full): " + ex);
                full = new List<FileJumpCandidate>();
            }

            Dispatcher.BeginInvoke(() =>
            {
                if (gen != _fileJumpAutoForegroundCollectGen) return;
                TryJumpFileDialogRefreshPickerIfOpen(dialogHwnd, full);
            }, DispatcherPriority.Normal);
        });
    }

    /// <summary>
    /// Shell.Application.Windows 会跨进程碰 Explorer；拖动文件对话框时触发会造成拖动卡顿。
    /// 完整采集不影响首屏弹出，等待鼠标释放并稳定后再扫。
    /// </summary>
    private static bool WaitForFileJumpFullCollectQuietWindow(IntPtr dialogHwnd, Func<bool> shouldAbort)
    {
        const int maxWaitMs = 5000;
        const int quietMs = 420;
        const int stepMs = 60;
        var waited = 0;
        var quietFor = 0;

        while (waited < maxWaitMs)
        {
            if (shouldAbort()) return false;

            var busy = IsPrimaryMouseButtonDown() || IsWindowThreadInMoveSize(dialogHwnd);
            if (busy)
                quietFor = 0;
            else
            {
                quietFor += stepMs;
                if (quietFor >= quietMs)
                    return true;
            }

            Thread.Sleep(stepMs);
            waited += stepMs;
        }

        ClipboardDiagnosticsLog.Write(
            $"filejump.perf full_collect_cancel_not_quiet hwnd=0x{dialogHwnd.ToInt64():X} waitedMs={waited}");
        return false;
    }

    private static bool IsPrimaryMouseButtonDown()
        => (Win32.GetAsyncKeyState(0x01) & 0x8000) != 0;

    private static bool IsWindowThreadInMoveSize(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero || !Win32.IsWindow(hwnd)) return false;
        var tid = Win32.GetWindowThreadProcessId(hwnd, out _);
        if (tid == 0) return false;
        var gti = new Win32.GUITHREADINFO { cbSize = Marshal.SizeOf<Win32.GUITHREADINFO>() };
        return Win32.GetGUIThreadInfo(tid, ref gti) && gti.hwndMoveSize != IntPtr.Zero;
    }

    private void TryJumpFileDialogRefreshPickerIfOpen(IntPtr dialogHwnd, List<FileJumpCandidate> full)
    {
        if (_activeFileJumpPicker == null) return;
        var root = Win32.GetAncestor(dialogHwnd, Win32.GA_ROOT);
        if (root == IntPtr.Zero || !ActivePickerMatchesDialog(root)) return;
        _activeFileJumpPicker.RefreshCandidatesFromExternal(full);
    }
}
