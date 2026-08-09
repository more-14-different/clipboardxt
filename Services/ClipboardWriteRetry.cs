using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ClipboardManager;

internal static class ClipboardWriteRetry
{
    private const int ClipbrdECantOpenHResult = unchecked((int)0x800401D0);

    internal readonly record struct Result(bool Success, bool ClipboardLocked);

    /// <summary>
    /// Uses async retry delays so clipboard contention does not block the UI thread.
    /// </summary>
    public static async Task<Result> TrySetDetailedAsync(
        Action setAction,
        string logOp,
        int maxRetries = 2,
        int delayMs = 40,
        IntPtr clipNudgeHwnd = default,
        Func<bool>? canContinueBeforeEachAttempt = null)
    {
        Exception? last = null;
        for (var i = 0; i < maxRetries; i++)
        {
            if (canContinueBeforeEachAttempt != null && !canContinueBeforeEachAttempt())
            {
                ClipboardDiagnosticsLog.Write($"TrySetClipboard aborted coherence op={logOp} attempt={i + 1}");
                return new Result(false, false);
            }

            try
            {
                Win32.CloseClipboard();
                setAction();
                if (i > 0)
                    ClipboardDiagnosticsLog.Write($"TrySetClipboard OK after retry i={i} op={logOp}");
                return new Result(true, false);
            }
            catch (Exception ex)
            {
                last = ex;
                var hr = ex is COMException com ? $" hr=0x{(uint)com.HResult:X8}" : "";
                ClipboardDiagnosticsLog.Write(
                    $"TrySetClipboard fail attempt={i + 1}/{maxRetries} op={logOp} {ex.GetType().Name}: {ex.Message}{hr}");
                if (IsClipboardCantOpen(ex))
                    ClipboardDiagnosticsLog.Write($"TrySetClipboard owner {DescribeOpenClipboardOwner(clipNudgeHwnd)}");
                if (i >= maxRetries - 1) break;

                if (canContinueBeforeEachAttempt != null && !canContinueBeforeEachAttempt())
                {
                    ClipboardDiagnosticsLog.Write($"TrySetClipboard aborted coherence before retry delay op={logOp}");
                    return new Result(false, IsClipboardCantOpen(ex));
                }

                if (IsClipboardCantOpen(ex) && clipNudgeHwnd != IntPtr.Zero && Win32.TryEmptyClipboardAfterOpen(clipNudgeHwnd))
                    ClipboardDiagnosticsLog.Write($"TrySetClipboard reNudge op={logOp} afterAttempt={i + 1}");
                await Task.Delay(delayMs);
            }
        }

        ClipboardDiagnosticsLog.Write($"TrySetClipboard GAVE_UP op={logOp} last={last?.GetType().Name}: {last?.Message}");
        return new Result(false, last is not null && IsClipboardCantOpen(last));
    }

    public static async Task<bool> TrySetAsync(
        Action setAction,
        string logOp,
        int maxRetries = 2,
        int delayMs = 40,
        IntPtr clipNudgeHwnd = default,
        Func<bool>? canContinueBeforeEachAttempt = null)
    {
        var result = await TrySetDetailedAsync(
            setAction,
            logOp,
            maxRetries,
            delayMs,
            clipNudgeHwnd,
            canContinueBeforeEachAttempt);
        return result.Success;
    }

    private static bool IsClipboardCantOpen(Exception ex) =>
        ex is COMException com && com.HResult == ClipbrdECantOpenHResult;

    private static string DescribeOpenClipboardOwner(IntPtr selfHwnd)
    {
        var owner = Win32.GetOpenClipboardWindow();
        if (owner == IntPtr.Zero)
            return "owner=NONE";

        var title = Win32.GetWindowText(owner);
        var cls = Win32.GetWindowClassName(owner);
        _ = Win32.GetWindowThreadProcessId(owner, out var pid);
        var procName = "";
        if (pid != 0)
        {
            try
            {
                using var p = Process.GetProcessById((int)pid);
                procName = p.ProcessName;
            }
            catch
            {
                procName = "?";
            }
        }

        var selfTag = owner == selfHwnd ? " self=True" : "";
        return $"owner=0x{owner.ToInt64():X} pid={pid} proc={procName} class={cls} title=\"{title}\"{selfTag}";
    }
}
