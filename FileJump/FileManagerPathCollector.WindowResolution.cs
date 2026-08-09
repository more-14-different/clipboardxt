using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace ClipboardManager;

internal static partial class FileManagerPathCollector
{
    private static List<IntPtr> GetTopLevelZOrderTopFirst()
    {
        var windows = new List<IntPtr>();
        for (var window = Win32.GetTopWindow(IntPtr.Zero);
             window != IntPtr.Zero;
             window = Win32.GetWindow(window, Win32.GW_HWNDNEXT))
        {
            if (Win32.IsWindow(window) && Win32.IsWindowVisible(window))
                windows.Add(window);
        }
        return windows;
    }

    /// <summary>对话框在 Z 序中向下偏移 <paramref name="zDelta"/> 个窗口后尝试取路径。</summary>
    public static string? TryGetZOrderLinkedFolder(
        IntPtr dialogHwnd,
        int zDelta = 2,
        bool allowBlockingExplorerRefresh = true,
        bool allowStaleExplorerCache = false,
        bool allowExplorerUiAutomation = true,
        bool allowBlockingSpecializedManagers = true)
    {
        if (dialogHwnd == IntPtr.Zero || zDelta < 1) return null;
        var windows = GetTopLevelZOrderTopFirst();
        var index = windows.IndexOf(dialogHwnd);
        if (index < 0) return null;
        var targetIndex = index + zDelta;
        if (targetIndex >= windows.Count) return null;
        return TryGetFolderForManagerHwnd(
            windows[targetIndex],
            allowBlockingExplorerRefresh,
            allowStaleExplorerCache,
            allowExplorerUiAutomation,
            allowBlockingSpecializedManagers);
    }

    /// <summary>尝试提取任意窗口所属文件管理器的当前路径。</summary>
    public static string? TryGetFolderForWindow(IntPtr hwnd, bool fresh = false)
    {
        if (hwnd == IntPtr.Zero || !Win32.IsWindow(hwnd)) return null;
        var root = Win32.GetAncestor(hwnd, Win32.GA_ROOT);
        if (root == IntPtr.Zero) root = hwnd;
        if (fresh)
        {
            var windowClass = Win32.GetWindowClassName(root);
            if (windowClass is "CabinetWClass" or "ExploreWClass")
                return TryGetExplorerPathForHwndFresh(root);
        }
        return TryGetFolderForManagerHwnd(root);
    }

    private static string? TryGetFolderForManagerHwnd(
        IntPtr window,
        bool allowBlockingExplorerRefresh = true,
        bool allowStaleExplorerCache = false,
        bool allowExplorerUiAutomation = true,
        bool allowBlockingSpecializedManagers = true)
    {
        var windowClass = Win32.GetWindowClassName(window);
        if (windowClass == "TTOTAL_CMD")
        {
            if (!allowBlockingSpecializedManagers) return null;
            return TryTotalCommanderPathFromClip(window, TcmCopySrcPathToClip, out var path) ? path : null;
        }
        if (windowClass == "ThunderRT6FormDC")
        {
            if (!allowBlockingSpecializedManagers) return null;
            if (TryGetProcessImagePath(window, null, out var executable)
                && Path.GetFileNameWithoutExtension(executable)
                    .Equals("xyplorer", StringComparison.OrdinalIgnoreCase))
            {
                return TryXyplorerPathFromClip(window, "::copytext get('path', a);", out var path)
                    ? path
                    : null;
            }
            return null;
        }
        if (windowClass is "CabinetWClass" or "ExploreWClass")
            return TryGetExplorerPathForHwnd(
                window,
                allowBlockingRefresh: allowBlockingExplorerRefresh,
                allowStaleCache: allowStaleExplorerCache,
                allowUiAutomation: allowExplorerUiAutomation);
        if (windowClass == "dopus.lister")
        {
            if (!allowBlockingSpecializedManagers) return null;
            return ParseDopusListerPaths(TryRunDopusInfoXml(window), window)
                .Select(candidate => candidate.path)
                .FirstOrDefault();
        }

        return allowBlockingSpecializedManagers
            ? TryGetFolderForAlternateUiManager(window)
            : null;
    }

    private static bool TryGetProcessImagePath(IntPtr hwnd, out string path) =>
        TryGetProcessImagePath(hwnd, null, out path);

    /// <summary>单次 <see cref="CollectCandidates"/> 内复用 PID→exe，避免重复 OpenProcess。</summary>
    private static bool TryGetProcessImagePath(
        IntPtr hwnd,
        Dictionary<uint, string>? executableByProcessId,
        out string path)
    {
        path = "";
        Win32.GetWindowThreadProcessId(hwnd, out var processId);
        if (executableByProcessId != null
            && executableByProcessId.TryGetValue(processId, out var cached))
        {
            path = cached;
            return !string.IsNullOrEmpty(path);
        }

        var process = Win32.OpenProcess(Win32.PROCESS_QUERY_LIMITED_INFORMATION, false, processId);
        if (process == IntPtr.Zero)
        {
            executableByProcessId?.TryAdd(processId, "");
            return false;
        }

        try
        {
            var buffer = new StringBuilder(1024);
            var found = Win32.GetModuleFileNameEx(process, IntPtr.Zero, buffer, buffer.Capacity) > 0
                        && File.Exists(path = buffer.ToString());
            if (!found) path = "";
            if (executableByProcessId != null) executableByProcessId[processId] = path;
            return found;
        }
        finally
        {
            Win32.CloseHandle(process);
        }
    }

    private sealed class ExplorerCabinetEnumState
    {
        public IntPtr Foreground;
        public uint ProcessId;
        public IntPtr Found;
    }

    private static bool EnumExplorerCabinetCallback(IntPtr window, IntPtr statePointer)
    {
        var state = (ExplorerCabinetEnumState)GCHandle.FromIntPtr(statePointer).Target!;
        if (window == IntPtr.Zero || !Win32.IsWindowVisible(window)) return true;
        var windowClass = Win32.GetWindowClassName(window);
        if (!windowClass.Equals("CabinetWClass", StringComparison.Ordinal)
            && !windowClass.Equals("ExploreWClass", StringComparison.Ordinal))
        {
            return true;
        }

        Win32.GetWindowThreadProcessId(window, out var processId);
        if (processId != state.ProcessId) return true;
        if (window == state.Foreground || Win32.IsChild(window, state.Foreground))
        {
            state.Found = window;
            return false;
        }

        return true;
    }

    private static IntPtr TryFindExplorerCabinetByEnumContains(IntPtr foreground)
    {
        Win32.GetWindowThreadProcessId(foreground, out var processId);
        var state = new ExplorerCabinetEnumState
        {
            Foreground = foreground,
            ProcessId = processId,
            Found = IntPtr.Zero,
        };
        var handle = GCHandle.Alloc(state);
        try
        {
            Win32.EnumWindows(EnumExplorerCabinetCallback, GCHandle.ToIntPtr(handle));
            return state.Found;
        }
        finally
        {
            handle.Free();
        }
    }

    /// <summary>自任意句柄沿父链查找资源管理器框架。</summary>
    public static IntPtr TryFindExplorerCabinetFrame(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero || !Win32.IsWindow(hwnd)) return IntPtr.Zero;
        const int maxHops = 64;
        var window = hwnd;
        for (var i = 0; i < maxHops && window != IntPtr.Zero; i++)
        {
            var windowClass = Win32.GetWindowClassName(window);
            if (windowClass.Equals("CabinetWClass", StringComparison.Ordinal)
                || windowClass.Equals("ExploreWClass", StringComparison.Ordinal))
            {
                return window;
            }
            window = Win32.GetParent(window);
        }

        return TryFindExplorerCabinetByEnumContains(hwnd);
    }

    public static string? TryGetExplorerFolderIfForeground(IntPtr foregroundHwnd)
    {
        if (foregroundHwnd == IntPtr.Zero || !Win32.IsWindow(foregroundHwnd)) return null;
        var frame = TryFindExplorerCabinetFrame(foregroundHwnd);
        if (frame == IntPtr.Zero) return null;
        return TryGetExplorerPathForHwnd(frame);
    }
}
