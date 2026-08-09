using System.Windows.Automation;

namespace ClipboardManager;

/// <summary>读取文件对话框当前目录。</summary>
internal static partial class FileDialogJumpHelper
{
    public static bool TryReadCurrentFolder(IntPtr hwnd, out string folder) =>
        TryReadCurrentFolder(hwnd, out folder, relaxed: false);

    /// <param name="relaxed">为 true 时扩大 UIA 扫描并尝试面包屑解析，便于自定义对话框探测。</param>
    public static bool TryReadCurrentFolder(IntPtr hwnd, out string folder, bool relaxed)
    {
        folder = "";
        var kind = ClassifyFileDialog(hwnd);

        // DLL 注入仅适用于文件对话框（#32770），不适用于 Explorer 窗口
        var className = Win32.GetWindowClassName(hwnd);
        var isExplorer = className is "CabinetWClass" or "ExploreWClass";
        if (!isExplorer)
        {
            // 最可靠方式：DLL 注入读取 IShellBrowser（适用于标准对话框）
            if (ShellDialogDeepNavigate.TryReadCurrentFolderInject(hwnd, out var injectFolder))
            {
                folder = injectFolder;
                return true;
            }
        }

        // UIA 回退（用于非标准对话框、Explorer 窗口、或注入失败时）
        try
        {
            var root = AutomationElement.FromHandle(hwnd);
            if (root == null) return false;

            var best = "";
            var q = new Queue<AutomationElement>();
            q.Enqueue(root);
            var seen = 0;
            var maxNodes = relaxed
                ? 500
                : kind == FileDialogKind.WpsCustom ? 450 : 150;
            var allowBreadcrumbInLoop = relaxed || kind == FileDialogKind.WpsCustom;
            while (q.Count > 0 && seen < maxNodes)
            {
                var el = q.Dequeue();
                seen++;
                try
                {
                    foreach (AutomationElement c in el.FindAll(TreeScope.Children, Condition.TrueCondition))
                        q.Enqueue(c);
                }
                catch { /* ignore */ }

                try
                {
                    if (el.TryGetCurrentPattern(ValuePattern.Pattern, out var vpObj))
                    {
                        var v = ((ValuePattern)vpObj).Current.Value;
                        if (TryNormalizeToExistingDirectory(v, out var norm) && best.Length == 0)
                            best = norm;
                    }
                }
                catch { }

                if (!allowBreadcrumbInLoop) continue;

                try
                {
                    var name = el.Current.Name;
                    if (TryWpsBreadcrumbTextToFolder(name, out var bc) && bc.Length > best.Length)
                        best = bc;
                }
                catch { }

                try
                {
                    var aid = el.Current.AutomationId;
                    if (!string.IsNullOrEmpty(aid)
                        && TryWpsBreadcrumbTextToFolder(aid, out var bc2) && bc2.Length > best.Length)
                        best = bc2;
                }
                catch { }
            }

            if (!string.IsNullOrEmpty(best))
            {
                folder = best;
                return true;
            }
        }
        catch { }

        if ((relaxed || kind == FileDialogKind.WpsCustom)
            && TryReadWpsBreadcrumbOnly(hwnd, out var wpsFolder))
        {
            folder = wpsFolder;
            return true;
        }

        return false;
    }

    /// <summary>专用于 ValuePattern 未给出完整路径时，仅从名称中的面包屑推断目录。</summary>
    private static bool TryReadWpsBreadcrumbOnly(IntPtr hwnd, out string folder)
    {
        folder = "";
        try
        {
            var root = AutomationElement.FromHandle(hwnd);
            if (root == null) return false;
            var best = "";
            var q = new Queue<AutomationElement>();
            q.Enqueue(root);
            for (var seen = 0; q.Count > 0 && seen < 500; seen++)
            {
                var el = q.Dequeue();
                try
                {
                    foreach (AutomationElement c in el.FindAll(TreeScope.Children, Condition.TrueCondition))
                        q.Enqueue(c);
                }
                catch { /* ignore */ }

                try
                {
                    var name = el.Current.Name;
                    if (TryWpsBreadcrumbTextToFolder(name, out var p) && p.Length > best.Length)
                        best = p;
                }
                catch { }
            }

            if (!string.IsNullOrEmpty(best))
            {
                folder = best;
                return true;
            }
        }
        catch { }
        return false;
    }
}
