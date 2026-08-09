using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace ClipboardManager;

/// <summary>通过 Shell COM、UIA 与缓存解析 Explorer 窗口路径。</summary>
internal static partial class FileManagerPathCollector
{
    private const int ShellExplorerEntriesCacheMs = 15000;
    private const int ShellExplorerEntriesQuickStaleCacheMs = 60000;

    private static readonly object s_shellExplorerEntriesCacheLock = new();
    private static readonly object s_shellExplorerEntriesEnumerateLock = new();
    private static List<ShellExplorerWindowEntry>? s_shellExplorerEntriesCache;
    private static long s_shellExplorerEntriesCacheTick;

    /// <summary>无缓存地读取 Explorer 窗口当前路径（用于轮询检测路径变化）。</summary>
    private static string? TryGetExplorerPathForHwndFresh(IntPtr explorerFrameHwnd)
    {
        if (explorerFrameHwnd == IntPtr.Zero) return null;
        var entries = TryEnumerateShellExplorerWindowsUncached();
        var (comPath, comMatchScore) = MatchBestComPathForExplorerFrameWithScore(explorerFrameHwnd, entries);

        string? uiaPath = null;
        if (comPath == null || comMatchScore < 4)
        {
            try
            {
                if (FileDialogJumpHelper.TryReadCurrentFolder(explorerFrameHwnd, out var jumpStyle, relaxed: comPath == null)
                    && !string.IsNullOrEmpty(jumpStyle))
                    uiaPath = jumpStyle;
            }
            catch { }
        }

        try { return MergeExplorerComPathWithUiPath(comPath, uiaPath); }
        catch { return uiaPath ?? comPath; }
    }

    /// <summary>单次 <see cref="CollectCandidates"/> 内复用：避免每个资源管理器窗口都全量遍历 Shell.Application.Windows。</summary>
    private readonly struct ShellExplorerWindowEntry
    {
        public ShellExplorerWindowEntry(IntPtr reportedHwnd, string path)
        {
            ReportedHwnd = reportedHwnd;
            Path = path;
        }

        public IntPtr ReportedHwnd { get; }
        public string Path { get; }
    }

    private static List<ShellExplorerWindowEntry> TryEnumerateShellExplorerWindows()
        => TryEnumerateShellExplorerWindows(out _, allowBlockingRefresh: true, allowStaleCache: false);

    private static List<ShellExplorerWindowEntry> TryEnumerateShellExplorerWindows(
        out bool cacheHit,
        bool allowBlockingRefresh,
        bool allowStaleCache)
    {
        cacheHit = false;
        if (TryGetShellExplorerEntriesCache(ShellExplorerEntriesCacheMs, out var cached))
        {
            cacheHit = true;
            return cached;
        }

        if (allowStaleCache
            && TryGetShellExplorerEntriesCache(ShellExplorerEntriesQuickStaleCacheMs, out cached))
        {
            cacheHit = true;
            return cached;
        }

        if (!allowBlockingRefresh)
            return new List<ShellExplorerWindowEntry>();

        lock (s_shellExplorerEntriesEnumerateLock)
        {
            if (TryGetShellExplorerEntriesCache(ShellExplorerEntriesCacheMs, out cached)
                || (allowStaleCache
                    && TryGetShellExplorerEntriesCache(ShellExplorerEntriesQuickStaleCacheMs, out cached)))
            {
                cacheHit = true;
                return cached;
            }

            var fresh = TryEnumerateShellExplorerWindowsUncached();
            lock (s_shellExplorerEntriesCacheLock)
            {
                s_shellExplorerEntriesCache = fresh;
                s_shellExplorerEntriesCacheTick = Environment.TickCount64;
            }

            return new List<ShellExplorerWindowEntry>(fresh);
        }
    }

    private static bool TryGetShellExplorerEntriesCache(int maxAgeMs, out List<ShellExplorerWindowEntry> entries)
    {
        entries = new List<ShellExplorerWindowEntry>();
        var now = Environment.TickCount64;
        lock (s_shellExplorerEntriesCacheLock)
        {
            if (s_shellExplorerEntriesCache == null
                || now - s_shellExplorerEntriesCacheTick < 0
                || now - s_shellExplorerEntriesCacheTick > maxAgeMs)
                return false;

            entries = new List<ShellExplorerWindowEntry>(s_shellExplorerEntriesCache);
            return true;
        }
    }

    private static List<ShellExplorerWindowEntry> TryEnumerateShellExplorerWindowsUncached()
    {
        var r = new List<ShellExplorerWindowEntry>();
        Type? t = Type.GetTypeFromProgID("Shell.Application");
        object? shell = null;
        object? windows = null;
        try
        {
            if (t == null) return r;
            shell = Activator.CreateInstance(t);
            if (shell == null) return r;
            windows = TryInvokeShellWindows(shell);
            if (windows == null) return r;
            var wt = windows.GetType();
            var countObj = wt.InvokeMember("Count", BindingFlags.GetProperty, null, windows, null);
            var count = Convert.ToInt32(countObj);
            for (var i = 0; i < count; i++)
            {
                object? win = null;
                try
                {
                    win = wt.InvokeMember("Item", BindingFlags.InvokeMethod, null, windows, new object[] { i });
                    if (win == null) continue;
                    var comHwnd = TryGetInternetExplorerHwnd(win);
                    var path = ReadShellWindowPath(win);
                    if (string.IsNullOrEmpty(path)) continue;
                    r.Add(new ShellExplorerWindowEntry(comHwnd, path));
                }
                finally
                {
                    if (win != null) Marshal.ReleaseComObject(win);
                }
            }
        }
        catch { /* COM 失败 */ }
        finally
        {
            if (windows != null) Marshal.ReleaseComObject(windows);
            if (shell != null) Marshal.ReleaseComObject(shell);
        }

        return r;
    }

    /// <returns>最佳 COM 路径与对应匹配分；无有效项时 bestScore 为 <see cref="int.MinValue"/>。</returns>
    private static (string? path, int bestScore) MatchBestComPathForExplorerFrameWithScore(IntPtr explorerFrameHwnd,
        List<ShellExplorerWindowEntry> entries)
    {
        if (entries.Count == 0) return (null, int.MinValue);
        var bestScore = int.MinValue;
        string? comPath = null;
        foreach (var e in entries)
        {
            var score = ExplorerComMatchScore(explorerFrameHwnd, e.ReportedHwnd);
            if (score < 0 || score < bestScore) continue;
            if (string.IsNullOrEmpty(e.Path)) continue;
            if (score > bestScore)
            {
                bestScore = score;
                comPath = e.Path;
            }
            else if (score == bestScore
                     && (string.IsNullOrEmpty(comPath) || e.Path.Length > comPath!.Length))
            {
                comPath = e.Path;
            }
        }

        return (comPath, bestScore);
    }

    /// <summary>
    /// 通过 Shell.Application.Windows 取资源管理器路径；必要时用 UIA 与 COM 合并（Win11 多标签等）。
    /// </summary>
    /// <param name="prebuiltShellEntries">非 null 时复用已枚举的 Shell 窗口（同一次 CollectCandidates 内多次 Cabinet 窗口只需一次 COM 全量扫描）。</param>
    private static string? TryGetExplorerPathForHwnd(IntPtr explorerFrameHwnd,
        List<ShellExplorerWindowEntry>? prebuiltShellEntries = null,
        bool allowBlockingRefresh = true,
        bool allowStaleCache = false,
        bool allowUiAutomation = true)
    {
        if (explorerFrameHwnd == IntPtr.Zero) return null;
        var (comPath, comMatchScore) = prebuiltShellEntries != null
            ? MatchBestComPathForExplorerFrameWithScore(explorerFrameHwnd, prebuiltShellEntries)
            : MatchBestComPathForExplorerFrameWithScore(
                explorerFrameHwnd,
                TryEnumerateShellExplorerWindows(
                    out _,
                    allowBlockingRefresh,
                    allowStaleCache));

        string? uiaPath = null;
        // Shell COM 快而 relaxed UIA 对 Explorer（Classify=None）可走满 500 节点 BFS；多窗叠加易达秒级。
        // COM 与 Shell 窗口 HWND 完全一致（分=4）时再扫整树多为重复。
        if (allowUiAutomation && (comPath == null || comMatchScore < 4))
        {
            var relaxedUia = comPath == null;
            if (comPath != null && comMatchScore < 4)
                relaxedUia = false;
            try
            {
                if (FileDialogJumpHelper.TryReadCurrentFolder(explorerFrameHwnd, out var jumpStyle, relaxed: relaxedUia)
                    && !string.IsNullOrEmpty(jumpStyle))
                    uiaPath = jumpStyle;
            }
            catch { /* ignore */ }
        }

        // Shell.Document 与前台地址栏在 Win11 上可能不一致；曾提早 return comPath 导致界面在 D:\gn 仍显示 C:\。
        try
        {
            return MergeExplorerComPathWithUiPath(comPath, uiaPath);
        }
        catch
        {
            if (!string.IsNullOrEmpty(uiaPath)) return uiaPath;
            return comPath;
        }
    }

    /// <summary>COM 路径与 UIA（与文件夹跳转同源）合并：盘符根误报、多标签错项时以界面为准。</summary>
    internal static string? MergeExplorerComPathWithUiPath(string? comPath, string? uiaPath)
    {
        if (string.IsNullOrEmpty(comPath)) return string.IsNullOrEmpty(uiaPath) ? null : uiaPath;
        if (string.IsNullOrEmpty(uiaPath)) return comPath;

        string c, u;
        try
        {
            c = Path.GetFullPath(comPath.Trim().TrimEnd('\\', '/'));
            u = Path.GetFullPath(uiaPath.Trim().TrimEnd('\\', '/'));
        }
        catch
        {
            return uiaPath;
        }

        if (string.Equals(c, u, StringComparison.OrdinalIgnoreCase))
            return comPath;

        if (u.StartsWith(c + "\\", StringComparison.OrdinalIgnoreCase))
            return uiaPath;
        if (c.StartsWith(u + "\\", StringComparison.OrdinalIgnoreCase))
            return comPath;

        if (ExplorerPathIsDriveLetterRootOnly(c) && !ExplorerPathIsDriveLetterRootOnly(u))
            return uiaPath;
        if (ExplorerPathIsDriveLetterRootOnly(u) && !ExplorerPathIsDriveLetterRootOnly(c))
            return comPath;

        return uiaPath;
    }

    private static bool ExplorerPathIsDriveLetterRootOnly(string normalizedFullPath)
    {
        try
        {
            var root = Path.GetPathRoot(normalizedFullPath);
            if (string.IsNullOrEmpty(root)) return false;
            return string.Equals(
                normalizedFullPath.TrimEnd('\\', '/'),
                root.TrimEnd('\\', '/'),
                StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static object? TryInvokeShellWindows(object shell)
    {
        var shellType = shell.GetType();
        try
        {
            return shellType.InvokeMember("Windows", BindingFlags.InvokeMethod, null, shell, null);
        }
        catch { /* 部分宿主上 Windows 为属性 */ }

        try
        {
            return shellType.InvokeMember("Windows", BindingFlags.GetProperty, null, shell, null);
        }
        catch { return null; }
    }

    private static IntPtr TryGetInternetExplorerHwnd(object win)
    {
        var t = win.GetType();
        foreach (var name in new[] { "HWND", "Hwnd" })
        {
            try
            {
                var v = t.InvokeMember(name, BindingFlags.GetProperty | BindingFlags.IgnoreCase, null, win, null);
                if (v == null) continue;
                var p = ComObjectToIntPtr(v);
                if (p != IntPtr.Zero) return p;
            }
            catch { /* ignore */ }
        }
        return IntPtr.Zero;
    }

    private static IntPtr ComObjectToIntPtr(object v)
    {
        if (v is IntPtr p) return p;
        try
        {
            return new IntPtr(Convert.ToInt64(v, System.Globalization.CultureInfo.InvariantCulture));
        }
        catch
        {
            return IntPtr.Zero;
        }
    }

    /// <returns>越高越可信；&lt;0 表示不应视为同一资源管理器窗口。</returns>
    private static int ExplorerComMatchScore(IntPtr explorerFrameHwnd, IntPtr shellReportedHwnd)
    {
        if (explorerFrameHwnd == IntPtr.Zero || shellReportedHwnd == IntPtr.Zero) return -1;
        if (explorerFrameHwnd == shellReportedHwnd) return 4;
        if (Win32.IsChild(explorerFrameHwnd, shellReportedHwnd)) return 3;
        for (var w = shellReportedHwnd; w != IntPtr.Zero; w = Win32.GetParent(w))
        {
            if (w == explorerFrameHwnd) return 2;
        }
        if (Win32.GetAncestor(shellReportedHwnd, Win32.GA_ROOT) == explorerFrameHwnd) return 1;
        var frameRoot = Win32.GetAncestor(explorerFrameHwnd, Win32.GA_ROOT);
        var shellRoot = Win32.GetAncestor(shellReportedHwnd, Win32.GA_ROOT);
        if (frameRoot != IntPtr.Zero && frameRoot == shellRoot) return 0;
        return -1;
    }

    private static string? ReadShellWindowPath(object win)
    {
        object? doc = null;
        object? folder = null;
        object? self = null;
        try
        {
            doc = win.GetType().InvokeMember("Document", BindingFlags.GetProperty, null, win, null);
            if (doc == null) return null;
            folder = doc.GetType().InvokeMember("Folder", BindingFlags.GetProperty, null, doc, null);
            if (folder == null) return null;
            self = folder.GetType().InvokeMember("Self", BindingFlags.GetProperty, null, folder, null);
            if (self == null) return null;
            var path = self.GetType().InvokeMember("Path", BindingFlags.GetProperty, null, self, null);
            return path?.ToString();
        }
        catch { return null; }
        finally
        {
            if (self != null) Marshal.ReleaseComObject(self);
            if (folder != null) Marshal.ReleaseComObject(folder);
            if (doc != null) Marshal.ReleaseComObject(doc);
        }
    }
}
