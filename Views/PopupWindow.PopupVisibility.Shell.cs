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
    /// <summary>
    /// 当前前台是否为开始菜单、搜索等 Shell 全屏层；用于相对 Z 序与定时刷新。
    /// </summary>
    private static bool IsShellForegroundWindow(IntPtr fg)
    {
        if (fg == IntPtr.Zero) return false;
        _ = Win32.GetWindowThreadProcessId(fg, out var pid);
        if (pid == 0) return false;
        try
        {
            using var p = Process.GetProcessById((int)pid);
            var name = p.ProcessName;
            if (IsDedicatedShellHostProcess(name))
                return true;

            // 任务栏搜索等：explorer.exe + WinUI CoreWindow（勿把 CabinetWClass 文件窗口当成 Shell）
            if (name.Equals("explorer", StringComparison.OrdinalIgnoreCase))
            {
                var cls = Win32.GetWindowClassName(fg);
                return cls.Equals("Windows.UI.Core.CoreWindow", StringComparison.OrdinalIgnoreCase);
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsDedicatedShellHostProcess(string name) =>
        name.Equals("SearchHost", StringComparison.OrdinalIgnoreCase)
        || name.Equals("StartMenuExperienceHost", StringComparison.OrdinalIgnoreCase)
        || name.Equals("ShellExperienceHost", StringComparison.OrdinalIgnoreCase)
        || name.Equals("ShellHost", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// 当前前台进程是否在排除列表中；若匹配则应跳过 ClipboardX 全局快捷键。
    /// </summary>
    internal static bool IsForegroundAppExcluded(AppSettings? settings)
    {
        if (settings == null || settings.ExclusionApps.Count == 0) return false;
        var fg = Win32.GetForegroundWindow();
        if (fg == IntPtr.Zero) return false;
        Win32.GetWindowThreadProcessId(fg, out uint pid);
        if (pid == 0) return false;
        try
        {
            using var proc = Process.GetProcessById((int)pid);
            var name = proc.ProcessName;
            return settings.ExclusionApps.Contains(name, StringComparer.OrdinalIgnoreCase);
        }
        catch { return false; }
    }
}
