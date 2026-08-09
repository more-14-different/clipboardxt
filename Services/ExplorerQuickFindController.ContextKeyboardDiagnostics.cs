using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace ClipboardManager;

public sealed partial class ExplorerQuickFindController : IDisposable
{    private static bool QuickCheckFocusNotEditBox(IntPtr explorerFrame)
    {
        var tid = Win32.GetWindowThreadProcessId(explorerFrame, out _);
        if (tid == 0) return false;

        var gti = new Win32.GUITHREADINFO { cbSize = Marshal.SizeOf<Win32.GUITHREADINFO>() };
        if (!Win32.GetGUIThreadInfo(tid, ref gti))
            return true; // 取不到就放行，让后续异步检测做最终判断

        var focus = gti.hwndFocus;
        if (focus == IntPtr.Zero) return true; // Win11 DirectUI 下偶发

        var cls = Win32.GetWindowClassName(focus);
        if (cls.Equals("Edit", StringComparison.Ordinal)) return false;
        if (cls.Contains("ComboBox", StringComparison.Ordinal)) return false;
        if (cls.Contains("RichEdit", StringComparison.OrdinalIgnoreCase)) return false;
        return true;
    }

    /// <summary>会话中快速判断前台是否仍是目标资源管理器。</summary>
    private bool IsStillTargetExplorer(IntPtr fg)
    {
        if (_sessionExplorerFrame == IntPtr.Zero) return false;
        if (fg == _sessionExplorerFrame) return true;
        var frame = FileManagerPathCollector.TryFindExplorerCabinetFrame(fg);
        return frame == _sessionExplorerFrame;
    }

    // ===================== 键盘状态工具 =====================

    private static void CaptureKeyState(Win32.KBDLLHOOKSTRUCT kb, out byte[] keyState)
    {
        keyState = new byte[256];
        Win32.GetKeyboardState(keyState);
    }

    private static bool TryGetChar(uint vk, uint scan, byte[] keyState, out char ch)
    {
        ch = '\0';
        var sb = new StringBuilder(8);
        var n = Win32.ToUnicode(vk, scan, keyState, sb, sb.Capacity, 0);
        if (n != 1 || sb.Length <= 0) return false;
        ch = sb[0];
        return !char.IsControl(ch);
    }

    // ===================== 错误格式化 =====================

    private static string FormatError(int err)
    {
        if (err == EverythingIpc.LastErrorDllNotFound)
            return "未找到 Everything64.dll。请把 Everything 安装目录下的 DLL 复制到 ClipboardX 同目录，或将该目录加入 PATH。";
        if (err == EverythingIpc.LastErrorInterop)
            return "调用 Everything 接口失败（体系结构或版本不匹配）。";
        return $"Everything 查询失败（错误码 {err}）。请确认 Everything 已运行且权限一致。";
    }

    // ===================== 诊断日志 =====================

    private static bool DiagEnabled =>
#if DEBUG
        true
#else
        string.Equals(Environment.GetEnvironmentVariable("CLIPBOARDX_DEBUG_EXPLORER_QF"), "1", StringComparison.Ordinal)
#endif
        ;

    private static void LogDiag(string message)
    {
        if (!DiagEnabled) return;
        var line = "[ExplorerQF] " + message;
        try
        {
            System.Diagnostics.Trace.WriteLine(line);
#if DEBUG
            System.Diagnostics.Debug.WriteLine(line);
#endif
            Win32.OutputDebugString(line + "\n");
        }
        catch { /* ignore */ }
    }

    private static void TryAppendLog(string detail)
    {
        try
        {
            File.AppendAllText(AppPaths.ExplorerQuickFindLogFile,
                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}  {detail}{Environment.NewLine}");
        }
        catch { /* ignore */ }
    }
}

