using System.Diagnostics;
using System.IO;
using System.Text;
using ClipboardManager.Models;

namespace ClipboardManager.Services;

internal sealed class ClipboardSourceTracker
{
    private readonly int _currentProcessId = Environment.ProcessId;
    private readonly object _gate = new();
    private Snapshot? _lastExternalForeground;
    private Snapshot? _lastCopyShortcut;

    public void UpdateForeground(IntPtr hwnd)
    {
        var snapshot = TryCreateSnapshot(hwnd, "foreground");
        if (snapshot == null || snapshot.ProcessId == _currentProcessId) return;
        lock (_gate)
            _lastExternalForeground = snapshot;
    }

    public void NoteCopyShortcut()
    {
        var hwnd = Win32.GetForegroundWindow();
        var snapshot = TryCreateSnapshot(hwnd, "ctrl_c");
        if (snapshot == null || snapshot.ProcessId == _currentProcessId) return;
        lock (_gate)
            _lastCopyShortcut = snapshot with { ClipboardSequence = Win32.GetClipboardSequenceNumber(), TickMs = Environment.TickCount64 };
    }

    public ClipboardSourceInfo? CaptureForClipboardUpdate()
    {
        var owner = TryCreateSnapshot(Win32.GetClipboardOwner(), "clipboard_owner");
        Snapshot? shortcut;
        Snapshot? lastForeground;
        lock (_gate)
        {
            shortcut = _lastCopyShortcut;
            lastForeground = _lastExternalForeground;
        }

        var now = Environment.TickCount64;
        if (IsUsable(owner))
            return owner!.ToInfo();

        if (shortcut != null && now - shortcut.TickMs is >= 0 and <= 2500 && IsUsable(shortcut))
            return shortcut.ToInfo("ctrl_c_snapshot");

        if (IsUsable(lastForeground))
            return lastForeground!.ToInfo("last_foreground");

        var foreground = TryCreateSnapshot(Win32.GetForegroundWindow(), "foreground_now");
        return IsUsable(foreground) ? foreground!.ToInfo() : null;
    }

    private bool IsUsable(Snapshot? snapshot)
    {
        if (snapshot == null) return false;
        if (snapshot.ProcessId == 0 || snapshot.ProcessId == _currentProcessId) return false;
        if (string.IsNullOrWhiteSpace(snapshot.ExeName)
            && string.IsNullOrWhiteSpace(snapshot.WindowTitle)
            && string.IsNullOrWhiteSpace(snapshot.WindowClass))
            return false;
        return true;
    }

    private static Snapshot? TryCreateSnapshot(IntPtr hwnd, string method)
    {
        if (hwnd == IntPtr.Zero || !Win32.IsWindow(hwnd)) return null;

        Win32.GetWindowThreadProcessId(hwnd, out var pid);
        var exePath = TryGetProcessPath(pid);
        var exeName = string.IsNullOrWhiteSpace(exePath)
            ? TryGetProcessName(pid)
            : Path.GetFileNameWithoutExtension(exePath);

        var focusHwnd = TryGetFocusedHwnd();
        var focusedClass = focusHwnd != IntPtr.Zero ? Win32.GetWindowClassName(focusHwnd) : "";

        return new Snapshot(
            Hwnd: hwnd,
            ProcessId: pid,
            AppName: exeName ?? "",
            ExeName: string.IsNullOrWhiteSpace(exePath) ? exeName ?? "" : Path.GetFileName(exePath),
            ExePath: exePath ?? "",
            WindowTitle: Win32.GetWindowText(hwnd),
            WindowClass: Win32.GetWindowClassName(hwnd),
            FocusedClass: focusedClass,
            CaptureMethod: method,
            ClipboardSequence: Win32.GetClipboardSequenceNumber(),
            TickMs: Environment.TickCount64);
    }

    private static IntPtr TryGetFocusedHwnd()
    {
        try
        {
            var info = new Win32.GUITHREADINFO { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<Win32.GUITHREADINFO>() };
            if (Win32.GetGUIThreadInfo(0, ref info))
                return info.hwndFocus != IntPtr.Zero ? info.hwndFocus : info.hwndActive;
        }
        catch
        {
            // ignore
        }

        return IntPtr.Zero;
    }

    private static string? TryGetProcessPath(uint pid)
    {
        if (pid == 0) return null;
        var hProc = Win32.OpenProcess(Win32.PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
        if (hProc == IntPtr.Zero) return null;
        try
        {
            var sb = new StringBuilder(1024);
            return Win32.GetModuleFileNameEx(hProc, IntPtr.Zero, sb, sb.Capacity) > 0
                ? sb.ToString()
                : null;
        }
        catch
        {
            return null;
        }
        finally
        {
            Win32.CloseHandle(hProc);
        }
    }

    private static string? TryGetProcessName(uint pid)
    {
        try
        {
            return Process.GetProcessById(unchecked((int)pid)).ProcessName;
        }
        catch
        {
            return null;
        }
    }

    private sealed record Snapshot(
        IntPtr Hwnd,
        uint ProcessId,
        string AppName,
        string ExeName,
        string ExePath,
        string WindowTitle,
        string WindowClass,
        string FocusedClass,
        string CaptureMethod,
        uint ClipboardSequence,
        long TickMs)
    {
        public ClipboardSourceInfo ToInfo(string? methodOverride = null) => new()
        {
            AppName = AppName,
            ExeName = ExeName,
            ExePath = ExePath,
            WindowTitle = WindowTitle,
            WindowClass = WindowClass,
            FocusedClass = FocusedClass,
            ProcessId = ProcessId,
            Hwnd = Hwnd.ToInt64(),
            CaptureMethod = methodOverride ?? CaptureMethod
        };
    }
}
