using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using WinForms = System.Windows.Forms;

namespace ClipboardManager;

public partial class App
{
#if DEBUG
    private async void StartWindowInspection()
    {
        _trayIcon?.ShowBalloonTip(2500, "ClipboardX",
            UiLanguage.T("3 秒后采集前台窗口信息，请切换到目标窗口…"), WinForms.ToolTipIcon.Info);
        await Task.Delay(3000);

        var hwnd = Win32.GetForegroundWindow();
        if (hwnd == IntPtr.Zero)
        {
            Dispatcher.Invoke(() =>
                LocalizedMessageBox.Show("未获取到前台窗口。", "采集窗口",
                    MessageBoxButton.OK, MessageBoxImage.Warning));
            return;
        }

        var info = CollectWindowInfo(hwnd);

        Dispatcher.Invoke(() =>
        {
            try { System.Windows.Clipboard.SetText(info); } catch { }
            LocalizedMessageBox.Show(
                info + "\n（已复制到剪贴板）",
                "ClipboardX 窗口信息采集",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        });
    }
#endif

    internal async void StartCustomFileDialogWizard()
    {
        _trayIcon?.ShowBalloonTip(2600, "ClipboardX",
            UiLanguage.T("3 秒后采集前台窗口并尝试多种跳转校验，请先打开目标文件对话框并切到该窗口…"),
            WinForms.ToolTipIcon.Info);
        await Task.Delay(3000);

        var hwnd = Win32.GetForegroundWindow();
        if (hwnd == IntPtr.Zero || !Win32.IsWindow(hwnd))
        {
            Dispatcher.Invoke(() =>
                LocalizedMessageBox.Show("未获取到前台窗口。", "自定义文件对话框",
                    MessageBoxButton.OK, MessageBoxImage.Warning));
            return;
        }

        if (FileDialogJumpHelper.ClassifyFileDialog(hwnd) != FileDialogKind.None)
        {
            Dispatcher.Invoke(() =>
                LocalizedMessageBox.Show(
                    "当前窗口已被内置识别为文件对话框（对话框识别不是 None），不会走自定义规则。\n请仅对内置识别为「无」的窗口使用本功能。",
                    "自定义文件对话框",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information));
            return;
        }

        var probePath = ResolveCustomDialogProbePath();
        if (string.IsNullOrEmpty(probePath) || !Directory.Exists(probePath))
        {
            Dispatcher.Invoke(() =>
                LocalizedMessageBox.Show(
                    "无法确定用于校验的有效文件夹路径。\n请先在任意已支持跳转的对话框里浏览到目标文件夹（更新「上次路径」），或复制某个已存在目录的完整路径到剪贴板后再试。",
                    "自定义文件对话框",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning));
            return;
        }

        var confirm = Dispatcher.Invoke(() =>
            LocalizedMessageBox.Show(
                "将依次尝试多种跳转方式，并通过读取当前路径判断是否已进入下列文件夹：\n\n" +
                probePath +
                "\n\n请确认该文件对话框当前**不在**此文件夹内，否则会误判。\n\n确定开始探测？",
                "自定义文件对话框",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Question));

        if (confirm != MessageBoxResult.OK) return;

        var rule = CustomFileDialogRule.CreateFromWindow(hwnd);
        rule.StrategyOrder = CustomFileDialogStore.DefaultStrategyOrder.ToList();

        var ok = Dispatcher.Invoke(() =>
            FileDialogJumpHelper.TryProbeCustomStrategies(hwnd, probePath, _settings.EnableShellNavigateInject, rule));

        CustomFileDialogStore.UpsertRule(rule);

        Dispatcher.Invoke(() =>
        {
            var msg = ok
                ? $"已保存。已锁定优先策略：{rule.PinnedStrategy}"
                : "已保存。未能自动校验出有效策略，跳转时将按顺序依次尝试。\n建议：把对话框切换到其他文件夹后，可从托盘再运行一次本向导。";
            LocalizedMessageBox.Show(msg, "自定义文件对话框", MessageBoxButton.OK,
                ok ? MessageBoxImage.Information : MessageBoxImage.Warning);
        });
    }

    private string? ResolveCustomDialogProbePath()
    {
        var mem = _settings.LastFileDialogFolder?.Trim();
        if (!string.IsNullOrEmpty(mem))
        {
            try
            {
                var full = Path.GetFullPath(mem);
                if (Directory.Exists(full)) return full;
            }
            catch { /* ignore */ }
        }

        try
        {
            var clip = System.Windows.Clipboard.GetText()?.Trim();
            if (string.IsNullOrEmpty(clip)) return null;
            var line = clip.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .FirstOrDefault(s => s.Length > 0);
            if (string.IsNullOrEmpty(line)) return null;
            var full = Path.GetFullPath(line);
            return Directory.Exists(full) ? full : null;
        }
        catch
        {
            return null;
        }
    }

#if DEBUG
    private static string CollectWindowInfo(IntPtr hwnd)
    {
        var className = Win32.GetWindowClassName(hwnd);
        var title = Win32.GetWindowText(hwnd);
        Win32.GetWindowThreadProcessId(hwnd, out var pid);

        var exeName = "(unknown)";
        var exeFullPath = "";
        if (pid != 0)
        {
            var hProc = Win32.OpenProcess(Win32.PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
            if (hProc != IntPtr.Zero)
            {
                try
                {
                    var sb = new StringBuilder(1024);
                    if (Win32.GetModuleFileNameEx(hProc, IntPtr.Zero, sb, sb.Capacity) > 0)
                    {
                        exeFullPath = sb.ToString();
                        exeName = Path.GetFileNameWithoutExtension(exeFullPath).ToLowerInvariant();
                    }
                }
                finally { Win32.CloseHandle(hProc); }
            }
        }

        var uiaName = "";
        try
        {
            var el = System.Windows.Automation.AutomationElement.FromHandle(hwnd);
            uiaName = el?.Current.Name ?? "";
        }
        catch { /* ignore */ }

        var kind = FileDialogJumpHelper.ClassifyFileDialog(hwnd);
        var customHit = CustomFileDialogStore.FindMatchingRule(hwnd);

        var childClasses = new List<string>();
        Win32.EnumChildWindows(hwnd, (ch, _) =>
        {
            if (childClasses.Count < 60)
                childClasses.Add(Win32.GetWindowClassName(ch));
            return true;
        }, IntPtr.Zero);

        var output = new StringBuilder();
        output.AppendLine("=== ClipboardX 窗口信息采集 ===");
        output.AppendLine($"句柄:     0x{hwnd.ToInt64():X}");
        output.AppendLine($"类名:     {className}");
        output.AppendLine($"标题:     {title}");
        output.AppendLine($"进程名:   {exeName}");
        output.AppendLine($"进程PID:  {pid}");
        output.AppendLine($"进程路径: {exeFullPath}");
        if (!string.IsNullOrEmpty(uiaName) && uiaName != title)
            output.AppendLine($"UIA名称: {uiaName}");
        output.AppendLine($"对话框识别: {kind}");
        if (kind == FileDialogKind.None)
        {
            if (customHit != null)
            {
                var pin = string.IsNullOrEmpty(customHit.PinnedStrategy) ? "按序尝试" : customHit.PinnedStrategy;
                output.AppendLine($"自定义跳转: 已保存（优先：{pin}）");
            }
            else
                output.AppendLine("自定义跳转: 未保存（设置 → 自定义文件对话框，或托盘向导）");
        }

        output.AppendLine();

        if (childClasses.Count > 0)
        {
            var grouped = childClasses.GroupBy(c => c).OrderByDescending(g => g.Count());
            output.AppendLine($"子窗口 ({childClasses.Count} 个):");
            foreach (var g in grouped)
                output.AppendLine(g.Count() > 1 ? $"  - {g.Key} (×{g.Count()})" : $"  - {g.Key}");
        }
        else
        {
            output.AppendLine("子窗口: (无)");
        }

        // Qt 等无子窗口时，浅层输出 UIA 子树帮助排查
        if (childClasses.Count == 0 || className.Contains("Qt", StringComparison.Ordinal))
        {
            try
            {
                var root = System.Windows.Automation.AutomationElement.FromHandle(hwnd);
                if (root != null)
                {
                    output.AppendLine();
                    output.AppendLine("UIA 子节点:");
                    var uiaChildren = root.FindAll(
                        System.Windows.Automation.TreeScope.Children,
                        System.Windows.Automation.Condition.TrueCondition);
                    foreach (System.Windows.Automation.AutomationElement child in uiaChildren)
                    {
                        try
                        {
                            var ct = child.Current.ControlType.ProgrammaticName.Replace("ControlType.", "");
                            var cn = child.Current.Name ?? "";
                            output.AppendLine($"  [{ct}] Name=\"{cn}\"");
                        }
                        catch { /* ignore */ }
                    }
                    if (uiaChildren.Count == 0)
                        output.AppendLine("  (无)");
                }
            }
            catch { /* ignore */ }
        }

        return output.ToString();
    }
#endif
}
