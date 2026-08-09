using System.IO;
using System.Windows.Automation;

namespace ClipboardManager;

/// <summary>通过受限 UI Automation 扫描第三方文件管理器路径。</summary>
internal static partial class FileManagerPathCollector
{
    /// <summary>其它白名单管理器：单路径、控节点数以降低 UIA 开销。</summary>
    private const int AlternateUiMaxNodesSingle = 400;

    /// <summary>Q-Dir 四格：多路径；采满即停。</summary>
    private const int QDirMaxDistinctPaths = 6;

    /// <summary>Q-Dir 仅在 Edit/Combo 快速通道后仍不足时再走的浅层 BFS 上限。</summary>
    private const int QDirFallbackMaxNodes = 160;

    private static readonly Condition s_editOrComboCondition = new OrCondition(
        new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit),
        new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.ComboBox));

    /// <summary>对 THESE 进程在类名未识别时走 UIA 弱匹配（exe 主名无扩展、不区分大小写）。</summary>
    private static readonly HashSet<string> AlternateUiPathProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        "freecommander",
        "doublecmd",
        "onecommander",
        "multicommander",
        "te64",
        "te32",
        "xplorer2",
        "speedcommander",
        "nomadnet",
        "files",
        "winnc",
        "fman",
    };

    private static bool ShouldUseAlternateUiAutomation(string procBaseName)
    {
        if (AlternateUiPathProcesses.Contains(procBaseName)) return true;
        // Microsoft Store「文件」等可能为 Files、Files!App 等变体
        if (procBaseName.StartsWith("files", StringComparison.OrdinalIgnoreCase)) return true;
        // Q-Dir.exe、Q-Dir_x64 等（SoftwareOK）
        if (procBaseName.StartsWith("q-dir", StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    /// <summary>无专用接口时：白名单进程 + 浅层 UIA 抓取已是真实目录的路径字符串。</summary>
    private static string? TryGetFolderForAlternateUiManager(IntPtr h)
    {
        if (!TryGetProcessImagePath(h, out var exe)) return null;
        var proc = Path.GetFileNameWithoutExtension(exe);
        if (!ShouldUseAlternateUiAutomation(proc)) return null;
        return TryFindBestFolderPathInAutomationTree(h, out var path) ? path : null;
    }

    private static string AlternateManagerDisplayLabel(string procFileBaseName, string exeFullPath)
    {
        var pl = procFileBaseName.ToLowerInvariant();
        return pl switch
        {
            "freecommander" => "FreeCommander",
            "doublecmd" => "Double Commander",
            "onecommander" => "OneCommander",
            "multicommander" => "Multi Commander",
            "te64" or "te32" => "Tablacus Explorer",
            "xplorer2" => "xplorer²",
            "speedcommander" => "SpeedCommander",
            "nomadnet" => "Nomad .NET",
            "winnc" => "WinNc",
            "fman" => "fman",
            var x when x.StartsWith("files", StringComparison.OrdinalIgnoreCase) => "Files",
            var x when x.StartsWith("q-dir", StringComparison.OrdinalIgnoreCase) => "Q-Dir",
            _ => Path.GetFileNameWithoutExtension(exeFullPath),
        };
    }

    private static bool TryFindBestFolderPathInAutomationTree(IntPtr hwnd, out string best)
    {
        best = "";
        var acc = "";
        try
        {
            var root = AutomationElement.FromHandle(hwnd);
            if (root == null) return false;

            var q = new Queue<AutomationElement>();
            q.Enqueue(root);
            for (var seen = 0; q.Count > 0 && seen < AlternateUiMaxNodesSingle; seen++)
            {
                var el = q.Dequeue();
                try
                {
                    foreach (AutomationElement c in el.FindAll(TreeScope.Children, Condition.TrueCondition))
                        q.Enqueue(c);
                }
                catch { /* ignore */ }

                ForEachUiStringOnElement(el, s => TryTakeLongerExistingDir(s, ref acc));
            }
        }
        catch
        {
            return false;
        }

        best = acc;
        return acc.Length > 0;
    }

    /// <summary>Q-Dir 多窗格：优先扫地址栏 Edit/Combo（UIA 原生枚举，避免整窗逐子结点 BFS）；不足再浅层补扫。</summary>
    private static List<string> CollectQDirFolderPathsFromAutomation(IntPtr hwnd)
    {
        var sink = new List<string>(6);
        var pathSeen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var root = AutomationElement.FromHandle(hwnd);
            if (root == null) return sink;

            AutomationElementCollection? edits = null;
            try
            {
                edits = root.FindAll(TreeScope.Descendants, s_editOrComboCondition);
            }
            catch { /* ignore */ }

            if (edits != null)
            {
                foreach (AutomationElement el in edits)
                {
                    ForEachUiStringOnElement(el, s =>
                    {
                        AddDistinctFolderPathsFromText(s, pathSeen, sink);
                    });
                    if (sink.Count >= QDirMaxDistinctPaths) return sink;
                }
            }

            if (sink.Count >= 3) return sink;

            QDirFallbackShallowBfs(root, pathSeen, sink);
        }
        catch { /* ignore */ }

        return sink;
    }

    private static void QDirFallbackShallowBfs(AutomationElement root, HashSet<string> pathSeen, List<string> sink)
    {
        var q = new Queue<AutomationElement>();
        q.Enqueue(root);
        for (var seen = 0; q.Count > 0 && seen < QDirFallbackMaxNodes && sink.Count < QDirMaxDistinctPaths; seen++)
        {
            var el = q.Dequeue();
            try
            {
                foreach (AutomationElement c in el.FindAll(TreeScope.Children, Condition.TrueCondition))
                    q.Enqueue(c);
            }
            catch { /* ignore */ }

            ForEachUiStringOnElement(el, s => AddDistinctFolderPathsFromText(s, pathSeen, sink));
        }
    }

    private static void ForEachUiStringOnElement(AutomationElement el, Action<string?> onText)
    {
        try
        {
            if (el.TryGetCurrentPattern(ValuePattern.Pattern, out var vpObj))
                onText(((ValuePattern)vpObj).Current.Value);
        }
        catch { /* ignore */ }

        try
        {
            if (el.TryGetCurrentPattern(TextPattern.Pattern, out var tpObj))
                onText(((TextPattern)tpObj).DocumentRange.GetText(-1));
        }
        catch { /* ignore */ }

        try
        {
            onText(el.Current.Name);
        }
        catch { /* ignore */ }

        try
        {
            onText(el.Current.HelpText);
        }
        catch { /* ignore */ }
    }

    private static void AddDistinctFolderPathsFromText(string? text, HashSet<string> pathSeen, List<string> sink)
    {
        if (string.IsNullOrEmpty(text) || sink.Count >= QDirMaxDistinctPaths) return;
        void TryAdd(string? normRaw)
        {
            if (string.IsNullOrEmpty(normRaw) || sink.Count >= QDirMaxDistinctPaths) return;
            string norm;
            try
            {
                norm = Path.GetFullPath(normRaw.TrimEnd('\\', '/'));
            }
            catch
            {
                return;
            }
            if (!Directory.Exists(norm)) return;
            if (!pathSeen.Add(norm)) return;
            sink.Add(norm);
        }

        if (FileDialogJumpHelper.TryNormalizeToExistingDirectory(text, out var n1))
            TryAdd(n1);
        if (sink.Count >= QDirMaxDistinctPaths) return;
        if (HasBreadcrumbArrow(text)
            && FileDialogJumpHelper.TryWpsBreadcrumbTextToFolder(text, out var n2))
            TryAdd(n2);
    }

    private static bool HasBreadcrumbArrow(string? text)
    {
        if (string.IsNullOrEmpty(text)) return false;
        return text.Contains('>') || text.Contains('＞') || text.Contains('›');
    }

    private static void TryTakeLongerExistingDir(string? text, ref string best)
    {
        if (string.IsNullOrEmpty(text)) return;
        if (FileDialogJumpHelper.TryNormalizeToExistingDirectory(text, out var norm) && norm.Length > best.Length)
            best = norm;
        if (!HasBreadcrumbArrow(text)) return;
        if (FileDialogJumpHelper.TryWpsBreadcrumbTextToFolder(text, out var crumb) && crumb.Length > best.Length)
            best = crumb;
    }
}
