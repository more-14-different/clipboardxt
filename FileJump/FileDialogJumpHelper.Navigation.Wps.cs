using System.IO;
using System.Text;
using System.Windows.Automation;

namespace ClipboardManager;

/// <summary>WPS 与 Qt 文件对话框的路径导航策略。</summary>
internal static partial class FileDialogJumpHelper
{
    /// <summary>
    /// WPS：无 IShellBrowser；含 ComboBoxEx/Edit、ReBar+F4 地址栏（与逍遥 QuickJump ChangePath 同类）、UIA、Alt+D、Ctrl+L。
    /// Qt5 窗口无 Win32 子控件，通过文件名输入框键入路径跳转。
    /// </summary>
    private static bool TryNavigateWpsCustom(IntPtr dialogHwnd, string folderPath)
    {
        if (!Directory.Exists(folderPath)) return false;
        var norm = Path.GetFullPath(folderPath);
        var folderWithSlash = norm.TrimEnd('\\', '/') + "\\";

        // 提前判断 Qt5：无 Win32 子控件时跳过所有 Win32/UIA 遍历（耗时会导致焦点丢失）
        if (!HasAnyWin32ChildWindow(dialogHwnd))
        {
            ShellNavigateLog.Write("wps",
                $"Qt5 dialog detected; class={Win32.GetWindowClassName(dialogHwnd)}");
            return TryNavigateQtFileDialog(dialogHwnd, folderWithSlash);
        }

        ActivateDialog(dialogHwnd);
        Thread.Sleep(100);

        if (TryWpsSetPathViaValuePattern(dialogHwnd, norm))
        {
            Thread.Sleep(60);
            SendEnter();
            Thread.Sleep(120);
            return true;
        }

        var comboEdit = FindComboBoxExEmbeddedEdit(dialogHwnd);
        if (comboEdit != IntPtr.Zero && TryWpsFillEditWithFolderAndEnter(comboEdit, folderWithSlash))
            return true;

        var bottomInput = FindBottomMostPathInputHwnd(dialogHwnd);
        if (bottomInput != IntPtr.Zero && TryWpsFillEditWithFolderAndEnter(bottomInput, folderWithSlash))
            return true;

        if (TryNavigateReBarF4AddressEdit(dialogHwnd, folderWithSlash, bottomInput))
            return true;

        SendAltD();
        Thread.Sleep(160);
        try
        {
            if (TrySetFocusedAddressValue(norm))
            {
                Thread.Sleep(50);
                SendEnter();
                Thread.Sleep(120);
                return true;
            }
        }
        catch { /* ignore */ }

        ShellNavigateLog.Write("wps",
            $"TryNavigateWpsCustom 回退 Ctrl+L；class={Win32.GetWindowClassName(dialogHwnd)} title={Win32.GetWindowText(dialogHwnd)}");
        SendCtrlL();
        Thread.Sleep(120);
        SendUnicodeString(norm);
        Thread.Sleep(50);
        SendEnter();
        Thread.Sleep(120);
        return true;
    }

    private static bool HasAnyWin32ChildWindow(IntPtr hwnd)
    {
        var found = false;
        Win32.EnumChildWindows(hwnd, (_, _) => { found = true; return false; }, IntPtr.Zero);
        return found;
    }

    /// <summary>
    /// WPS Qt5 文件对话框跳转：先确保前台焦点，然后依次尝试 Alt+N / 直接输入。
    /// </summary>
    private static bool TryNavigateQtFileDialog(IntPtr dialogHwnd, string folderWithSlash)
    {
        ActivateDialog(dialogHwnd);
        Thread.Sleep(50);
        SendAltN();
        Thread.Sleep(30);
        SendCtrlA();
        Thread.Sleep(10);
        SendUnicodeString(folderWithSlash);
        Thread.Sleep(10);
        SendEnter();
        return true;
    }

    /// <summary>commctrl ComboBoxEx32 内嵌编辑框，常见于地址栏。</summary>
    private static IntPtr FindComboBoxExEmbeddedEdit(IntPtr root)
    {
        IntPtr found = IntPtr.Zero;
        const uint cbemGetEditControl = 0x0400 + 102;
        void Walk(IntPtr h)
        {
            if (found != IntPtr.Zero) return;
            if (string.Equals(Win32.GetWindowClassName(h), "ComboBoxEx32", StringComparison.OrdinalIgnoreCase))
            {
                var edit = Win32.SendMessage(h, cbemGetEditControl, IntPtr.Zero, IntPtr.Zero);
                if (edit != IntPtr.Zero)
                    found = edit;
                return;
            }
            Win32.EnumChildWindows(h, (c, _) =>
            {
                Walk(c);
                return true;
            }, IntPtr.Zero);
        }
        Walk(root);
        return found;
    }

    private static bool TryWpsFillEditWithFolderAndEnter(IntPtr hwnd, string folderWithSlash)
    {
        if (hwnd == IntPtr.Zero) return false;
        try
        {
            var cap = (int)(long)Win32.SendMessage(hwnd, Win32.WM_GETTEXTLENGTH, IntPtr.Zero, IntPtr.Zero);
            if (cap < 0) cap = 0;
            cap = Math.Min(cap + 64, 32767);
            var oldSb = new StringBuilder(Math.Max(cap, 256));
            Win32.SendMessage(hwnd, Win32.WM_GETTEXT, (IntPtr)oldSb.Capacity, oldSb);
            var oldText = oldSb.ToString();

            Win32.SendMessage(hwnd, Win32.WM_SETTEXT, IntPtr.Zero, folderWithSlash);
            Win32.SetFocus(hwnd);
            Thread.Sleep(40);
            SendEnter();
            Thread.Sleep(120);

            Win32.SendMessage(hwnd, Win32.WM_SETTEXT, IntPtr.Zero, oldText);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryWpsSetPathViaValuePattern(IntPtr dialogHwnd, string fullPath)
    {
        try
        {
            var root = AutomationElement.FromHandle(dialogHwnd);
            if (root == null) return false;
            var candidates = new List<(int score, AutomationElement el)>();
            var q = new Queue<AutomationElement>();
            q.Enqueue(root);
            for (var seen = 0; q.Count > 0 && seen < 450; seen++)
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
                    if (!el.TryGetCurrentPattern(ValuePattern.Pattern, out var vpObj))
                        continue;
                    var vp = (ValuePattern)vpObj;
                    var score = WpsValuePatternCandidateScore(el, vp);
                    if (score < 0)
                        continue;
                    candidates.Add((score, el));
                }
                catch { /* ignore */ }
            }

            foreach (var (_, el) in candidates.OrderByDescending(t => t.score))
            {
                try
                {
                    if (!el.TryGetCurrentPattern(ValuePattern.Pattern, out var useVp))
                        continue;
                    var v = (ValuePattern)useVp;
                    if (v.Current.IsReadOnly)
                        continue;
                    v.SetValue(fullPath);
                    return true;
                }
                catch { /* ignore */ }
            }

            // 部分 Qt/自定义提供程序误报 IsReadOnly，第二轮不区分只读位一律尝试
            foreach (var (_, el) in candidates.OrderByDescending(t => t.score))
            {
                try
                {
                    if (!el.TryGetCurrentPattern(ValuePattern.Pattern, out var useVp))
                        continue;
                    ((ValuePattern)useVp).SetValue(fullPath);
                    return true;
                }
                catch { /* ignore */ }
            }
        }
        catch { /* ignore */ }

        return false;
    }

    /// <summary>分数越高越可能是地址/路径框；返回 -1 表示不参与。</summary>
    private static int WpsValuePatternCandidateScore(AutomationElement el, ValuePattern vp)
    {
        var score = 0;
        try
        {
            if (!vp.Current.IsReadOnly)
                score += 2;
            var curVal = vp.Current.Value ?? "";
            if (curVal.Contains(':', StringComparison.Ordinal) && (curVal.Contains('\\') || curVal.Contains('/')))
                score += 3;
        }
        catch { /* ignore */ }

        try
        {
            var n = el.Current.Name ?? "";
            if (n.Contains("地址", StringComparison.Ordinal)
                || n.Contains("路径", StringComparison.Ordinal)
                || n.Contains("位置", StringComparison.Ordinal))
                score += 6;
            if (n.Contains("文件夹", StringComparison.Ordinal))
                score += 4;
            if (n.Contains("文件", StringComparison.Ordinal) && n.Contains("名", StringComparison.Ordinal))
                score += 1;
        }
        catch { /* ignore */ }

        return score > 0 ? score : -1;
    }
}
