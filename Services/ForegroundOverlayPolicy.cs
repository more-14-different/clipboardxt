using System.Diagnostics;

namespace ClipboardManager;

/// <summary>识别不应被当作用户切换目标的辅助工具前台窗口。</summary>
internal static class ForegroundOverlayPolicy
{
    public static bool ShouldIgnoreForegroundWindow(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero || !Win32.IsWindow(hwnd)) return false;

        Win32.GetWindowThreadProcessId(hwnd, out var pid);
        if (pid == 0) return false;
        try
        {
            using var process = Process.GetProcessById((int)pid);
            return IsMousemasterProcessName(process.ProcessName);
        }
        catch
        {
            return false;
        }
    }

    internal static bool IsMousemasterProcessName(string? processName) =>
        processName?.Equals("mousemaster", StringComparison.OrdinalIgnoreCase) == true;
}
