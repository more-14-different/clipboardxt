using System.IO;
using System.Text;

namespace ClipboardManager;

internal enum FileDialogKind
{
    None,
    /// <summary>#32770 + Shell 视图，或未检出子类型时的通用处理（地址栏）。</summary>
    ShellDefViewOrGeneral,
    /// <summary>DirectUIHWND + ToolbarWindow32 + Edit（较新通用对话框）。</summary>
    GeneralDirectUi,
    /// <summary>SysListView32 + ToolbarWindow32 + Edit（如部分旧式宿主）。</summary>
    SysListView,
    /// <summary>WPS 办公组件（wps/et/wpp 等）自带的「打开文件 / 另存为」等非 #32770 对话框。</summary>
    WpsCustom,
}

internal sealed class FileDialogClassSummary
{
    private bool _hasDirectUi;
    private bool _hasList;
    private bool _hasToolbar;
    private bool _hasEdit;
    private bool _hasShellDefView;

    /// <summary>
    /// Records one window class and returns whether enumeration still needs to continue.
    /// GeneralDirectUi is the highest-priority class-based result, so it is safe to stop once complete.
    /// </summary>
    internal bool Observe(string className)
    {
        if (className.Contains("DirectUIHWND", StringComparison.Ordinal))
            _hasDirectUi = true;
        if (string.Equals(className, "SysListView32", StringComparison.OrdinalIgnoreCase))
            _hasList = true;
        if (string.Equals(className, "ToolbarWindow32", StringComparison.OrdinalIgnoreCase))
            _hasToolbar = true;
        if (string.Equals(className, "Edit", StringComparison.OrdinalIgnoreCase))
            _hasEdit = true;
        if (string.Equals(className, "SHELLDLL_DefView", StringComparison.Ordinal))
            _hasShellDefView = true;

        return Classify() != FileDialogKind.GeneralDirectUi;
    }

    internal FileDialogKind Classify()
    {
        if (_hasDirectUi && _hasToolbar && _hasEdit)
            return FileDialogKind.GeneralDirectUi;
        if (_hasList && _hasToolbar && _hasEdit)
            return FileDialogKind.SysListView;
        if (_hasShellDefView)
            return FileDialogKind.ShellDefViewOrGeneral;
        return FileDialogKind.None;
    }
}

/// <summary>识别文件对话框类型，对齐 QuickSwitch 的 SysListView / DirectUI 启发式。</summary>
internal static partial class FileDialogJumpHelper
{
    public static FileDialogKind ClassifyFileDialog(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero || !Win32.IsWindow(hwnd)) return FileDialogKind.None;
        if (IsKnownNonFileDialogTitle(Win32.GetWindowText(hwnd))) return FileDialogKind.None;

        // WPS 不使用系统公共 #32770 对话框，须在类名判断之前识别。
        if (IsWpsSuiteFileDialog(hwnd))
            return FileDialogKind.WpsCustom;

        if (!Win32.GetWindowClassName(hwnd).Equals("#32770", StringComparison.Ordinal))
            return FileDialogKind.None;

        // Internet Download Manager 主界面为 #32770 + Explorer 风格子控件，易误判为公共对话框，
        // 且会触发下方整树 Class 收集导致明显卡顿。无 owner 的顶层且标题不像打开/保存时视为其主壳，不参与跳转。
        if (TryGetExeBaseNameLower(hwnd, out var idmExe) && idmExe == "idman"
            && Win32.GetWindow(hwnd, Win32.GW_OWNER) == IntPtr.Zero
            && !IsFileDialogTitle(Win32.GetWindowText(hwnd)))
            return FileDialogKind.None;

        var descendantKind = CollectDescendantClassSummary(hwnd).Classify();
        if (descendantKind != FileDialogKind.None) return descendantKind;
        if (IsFileDialogTitle(Win32.GetWindowText(hwnd))) return FileDialogKind.ShellDefViewOrGeneral;
        return FileDialogKind.None;
    }

    public static bool IsLikelyFileDialog(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero || !Win32.IsWindow(hwnd)) return false;
        if (IsKnownNonFileDialogTitle(Win32.GetWindowText(hwnd))) return false;
        if (ClassifyFileDialog(hwnd) != FileDialogKind.None) return true;
        return CustomFileDialogStore.FindMatchingRule(hwnd) != null;
    }

    /// <summary>
    /// 部分宿主（如微信）将键盘焦点落在对话框内子控件上时，<see cref="Win32.GetForegroundWindow"/> 可能返回子 HWND，
    /// 而 <see cref="ClassifyFileDialog"/> 只对 #32770 等顶层成立。沿 GetParent 链上溯直至找到可对 <see cref="IsLikelyFileDialog"/> 成立的窗口。
    /// </summary>
    public static IntPtr ResolveFileDialogHwndFromWindowOrAncestor(IntPtr start)
    {
        if (start == IntPtr.Zero || !Win32.IsWindow(start)) return IntPtr.Zero;
        var h = start;
        for (var i = 0; i < 64 && h != IntPtr.Zero; i++)
        {
            if (IsLikelyFileDialog(h))
                return h;
            h = Win32.GetParent(h);
        }

        var root = Win32.GetAncestor(start, Win32.GA_ROOT);
        if (root != IntPtr.Zero)
        {
            // 微信等：前台事件里的 HWND 常仍是主窗，模态「打开文件」在 GetLastActivePopup(主窗) 上。
            Span<IntPtr> owners = root != start
                ? stackalloc IntPtr[] { start, root }
                : stackalloc IntPtr[] { start };
            foreach (var owner in owners)
            {
                if (owner == IntPtr.Zero) continue;
                var popup = Win32.GetLastActivePopup(owner);
                if (popup != IntPtr.Zero
                    && popup != owner
                    && Win32.IsWindow(popup)
                    && IsLikelyFileDialog(popup))
                    return popup;
            }

            if (root != start && IsLikelyFileDialog(root))
                return root;
        }

        return IntPtr.Zero;
    }

    /// <summary>
    /// 仅做轻量判断（沿父链比类名、看 <see cref="Win32.GetLastActivePopup"/>），供高频焦点钩过滤；
    /// 不做 <see cref="ClassifyFileDialog"/>，避免在全局焦点事件上整树枚举子控件。
    /// </summary>
    public static bool QuickMayBeUnderFileDialog(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero || !Win32.IsWindow(hwnd)) return false;
        var h = hwnd;
        for (var i = 0; i < 14 && h != IntPtr.Zero; i++)
        {
            if (Win32.GetWindowClassName(h).Equals("#32770", StringComparison.Ordinal))
                return true;
            h = Win32.GetParent(h);
        }

        var root = Win32.GetAncestor(hwnd, Win32.GA_ROOT);
        if (root == IntPtr.Zero) return false;
        var pop = Win32.GetLastActivePopup(root);
        return pop != IntPtr.Zero
               && pop != root
               && Win32.IsWindow(pop)
               && Win32.GetWindowClassName(pop).Equals("#32770", StringComparison.Ordinal);
    }

    /// <summary>进程主模块基名（小写，无扩展名），用于识别 WPS 套件。</summary>
    private static bool TryGetExeBaseNameLower(IntPtr hwnd, out string name)
    {
        name = "";
        Win32.GetWindowThreadProcessId(hwnd, out var pid);
        if (pid == 0) return false;
        var h = Win32.OpenProcess(Win32.PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
        if (h == IntPtr.Zero) return false;
        try
        {
            var sb = new StringBuilder(1024);
            if (Win32.GetModuleFileNameEx(h, IntPtr.Zero, sb, sb.Capacity) == 0)
                return false;
            name = Path.GetFileNameWithoutExtension(sb.ToString()).ToLowerInvariant();
            return name.Length > 0;
        }
        finally
        {
            Win32.CloseHandle(h);
        }
    }

    private static bool IsWpsSuiteExe(string exeBaseLower) =>
        exeBaseLower is "wps" or "et" or "wpp" or "pdf" or "ksolaunch";

    /// <summary>WPS 自定义打开/保存窗口标题（需与套件进程同时匹配，避免误伤其他「打开」对话框）。</summary>
    private static bool IsWpsFileDialogTitle(string title)
    {
        if (string.IsNullOrEmpty(title)) return false;
        if (title.Contains("打开文件", StringComparison.Ordinal)) return true;
        if (title.Contains("打开文档", StringComparison.Ordinal)) return true;
        if (title.Contains("打开工作簿", StringComparison.Ordinal)) return true;
        if (title.Contains("打开演示", StringComparison.Ordinal)) return true;
        if (title.Contains("另存文件", StringComparison.Ordinal)) return true;
        if (title.Contains("保存文件", StringComparison.Ordinal)) return true;
        if (title.Contains("选择文件", StringComparison.Ordinal)) return true;
        if (title.Contains("选取文件", StringComparison.Ordinal)) return true;
        if (title.Contains("浏览文件夹", StringComparison.Ordinal)) return true;
        // 简短标题（部分语言包 / 新版本）
        if (title.Equals("另存为", StringComparison.Ordinal)) return true;
        if (title.Equals("保存", StringComparison.Ordinal)) return true;
        if (title.Equals("打开", StringComparison.Ordinal)) return true;
        if (title.StartsWith("打开(", StringComparison.Ordinal)) return true;
        if (title.StartsWith("另存为(", StringComparison.Ordinal)) return true;
        var t = title.ToLowerInvariant();
        if (t.Contains("save as")) return true;
        if (t.Contains("open file")) return true;
        if (t.Contains("browse")) return true;
        if (t.Contains("select file")) return true;
        if (t is "open" or "save") return true;
        return false;
    }

    /// <summary>Qt 壳 + 极短本地化标题时补充匹配（仍要求 WPS 进程）。</summary>
    private static bool IsWpsQtLikeWindowClass(string className)
    {
        if (string.IsNullOrEmpty(className)) return false;
        return className.Contains("Qt", StringComparison.Ordinal)
               || className.Contains("QWindow", StringComparison.Ordinal);
    }

    private static bool IsWpsSuiteFileDialog(IntPtr hwnd)
    {
        if (!TryGetExeBaseNameLower(hwnd, out var exe) || !IsWpsSuiteExe(exe))
            return false;

        var className = Win32.GetWindowClassName(hwnd);

        // #32770 也归入 WpsCustom（后备方法更丰富），注入由 TryNavigateToFolder 按类名单独处理
        if (className.Equals("#32770", StringComparison.Ordinal))
            return true;

        var title = Win32.GetWindowText(hwnd);
        if (IsWpsFileDialogTitle(title))
            return true;

        if (!IsWpsQtLikeWindowClass(className))
            return false;

        if (!string.IsNullOrEmpty(title)
            && (title.Contains("打开", StringComparison.Ordinal)
                || title.Contains("另存", StringComparison.Ordinal)
                || title.Contains("保存", StringComparison.Ordinal)))
            return true;

        // WPS Qt5 自绘对话框：GetWindowText 为空且 UIA 不可用。
        // WPS 主窗口/首页/新建页同样是空标题 Qt 窗口，需排除：
        // 文件对话框由主窗口弹出，有 owner；主窗口/首页无 owner。
        if (string.IsNullOrEmpty(title)
            && Win32.GetWindow(hwnd, Win32.GW_OWNER) != IntPtr.Zero)
            return true;

        return false;
    }

    private static FileDialogClassSummary CollectDescendantClassSummary(IntPtr root)
    {
        var summary = new FileDialogClassSummary();
        var nodeCount = 0;
        const int maxNodes = 500;

        bool ObserveWindow(IntPtr hwnd)
        {
            if (++nodeCount > maxNodes)
                return false;
            return summary.Observe(Win32.GetWindowClassName(hwnd));
        }

        if (ObserveWindow(root))
        {
            // EnumChildWindows already walks all descendants. Recursing from every callback repeats
            // whole subtrees and previously blocked the WPF dispatcher for seconds on large dialogs.
            Win32.EnumChildWindows(root, (child, _) =>
            {
                return ObserveWindow(child);
            }, IntPtr.Zero);
        }

        return summary;
    }

    private static bool IsFileDialogTitle(string title)
    {
        if (string.IsNullOrEmpty(title)) return false;
        if (IsKnownNonFileDialogTitle(title)) return false;
        var t = title.ToLowerInvariant();
        return title.Contains("打开", StringComparison.Ordinal)
               || title.Contains("另存", StringComparison.Ordinal)
               || title.Contains("保存", StringComparison.Ordinal)
               || t.Contains("open file", StringComparison.Ordinal)
               || t.Contains("open folder", StringComparison.Ordinal)
               || t.Equals("open", StringComparison.Ordinal)
               || t.Contains("save as", StringComparison.Ordinal)
               || t.Equals("save", StringComparison.Ordinal)
               || t.Contains("browse", StringComparison.Ordinal);
    }

    internal static bool IsKnownNonFileDialogTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title)) return false;
        var t = title.Trim().ToLowerInvariant();

        // Windows Attachment Manager 的运行确认框同样使用 #32770，标题还会命中
        // IsFileDialogTitle 的「打开文件 / open file」兜底，但它不是文件选择窗口。
        // 同时要求「打开文件」和「安全警告」，避免排除其他类型的安全提示。
        if (title.Contains("打开文件", StringComparison.Ordinal)
            && title.Contains("安全警告", StringComparison.Ordinal))
            return true;
        if (t.Contains("open file", StringComparison.Ordinal)
            && t.Contains("security warning", StringComparison.Ordinal))
            return true;

        // Sublime Text 等编辑器的保存确认框是 #32770，标题含 save，但不是文件对话框。
        if (t.Contains("save changes", StringComparison.Ordinal)) return true;
        if (t.Contains("unsaved changes", StringComparison.Ordinal)) return true;
        if (t.Contains("do you want to save", StringComparison.Ordinal)) return true;
        if (t.Contains("confirm save", StringComparison.Ordinal)) return true;

        if (title.Contains("保存更改", StringComparison.Ordinal)) return true;
        if (title.Contains("是否保存", StringComparison.Ordinal)) return true;
        if (title.Contains("保存修改", StringComparison.Ordinal)) return true;
        if (title.Contains("未保存", StringComparison.Ordinal)) return true;
        return false;
    }
}
