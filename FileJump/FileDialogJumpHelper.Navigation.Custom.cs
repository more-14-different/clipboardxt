using System.IO;

namespace ClipboardManager;

/// <summary>自定义文件对话框规则的策略排序、探测与分派。</summary>
internal static partial class FileDialogJumpHelper
{
    /// <summary>
    /// 在对话框当前不处于 <paramref name="folderPath"/> 时，依次尝试策略并用宽松 UIA 读取校验；
    /// 成功则将 <paramref name="rule"/>.<see cref="CustomFileDialogRule.PinnedStrategy"/> 设为命中项。
    /// </summary>
    public static bool TryProbeCustomStrategies(
        IntPtr dialogHwnd,
        string folderPath,
        bool allowShellInject,
        CustomFileDialogRule rule)
    {
        if (dialogHwnd == IntPtr.Zero || !Win32.IsWindow(dialogHwnd)) return false;
        var norm = NormalizeFolderPathForNavigation(folderPath);
        if (!Directory.Exists(norm)) return false;

        if (TryReadCurrentFolder(dialogHwnd, out var before, relaxed: true)
            && PathsLooselyEqual(before, norm))
        {
            ShellNavigateLog.Write("custom_fd",
                "probe skipped: dialog already at target path (请切换到其他文件夹后再探测)");
            return false;
        }

        var order = rule.StrategyOrder is { Count: > 0 }
            ? rule.StrategyOrder.Where(s => !string.IsNullOrEmpty(s)).ToList()
            : CustomFileDialogStore.DefaultStrategyOrder.ToList();

        foreach (var s in order)
        {
            TryApplyCustomDialogStrategy(s, dialogHwnd, norm, allowShellInject);
            Thread.Sleep(480);
            if (TryReadCurrentFolder(dialogHwnd, out var after, relaxed: true)
                && PathsLooselyEqual(after, norm))
            {
                rule.PinnedStrategy = s;
                ShellNavigateLog.Write("custom_fd", $"probe pinned strategy={s}");
                return true;
            }
        }

        rule.PinnedStrategy = null;
        return false;
    }

    internal static List<string> BuildCustomStrategyOrder(CustomFileDialogRule rule)
    {
        var source = rule.StrategyOrder is { Count: > 0 }
            ? rule.StrategyOrder
            : CustomFileDialogStore.DefaultStrategyOrder.ToList();
        var order = new List<string>();
        if (!string.IsNullOrEmpty(rule.PinnedStrategy)
            && source.Contains(rule.PinnedStrategy, StringComparer.OrdinalIgnoreCase))
            order.Add(rule.PinnedStrategy);
        foreach (var s in source)
        {
            if (string.IsNullOrEmpty(s)) continue;
            if (order.Contains(s, StringComparer.OrdinalIgnoreCase)) continue;
            order.Add(s);
        }

        return order;
    }

    private static bool TryNavigateCustomRule(
        IntPtr dialogHwnd,
        string path,
        bool allowShellInject,
        CustomFileDialogRule rule)
    {
        foreach (var s in BuildCustomStrategyOrder(rule))
        {
            if (TryApplyCustomDialogStrategy(s, dialogHwnd, path, allowShellInject))
                return true;
        }

        return false;
    }

    private static bool TryApplyCustomDialogStrategy(
        string strategyId,
        IntPtr dialogHwnd,
        string normalizedExistingDir,
        bool allowShellInject)
    {
        var id = strategyId.Trim().ToLowerInvariant();
        try
        {
            switch (id)
            {
                case "shell_inject":
                    if (!allowShellInject) return false;
                    if (!Win32.GetWindowClassName(dialogHwnd).Equals("#32770", StringComparison.Ordinal))
                        return false;
                    return ShellDialogDeepNavigate.TryBrowseObjectInject(dialogHwnd, normalizedExistingDir);
                case "sys_listview":
                    return TryNavigateSysListViewStyle(dialogHwnd, normalizedExistingDir);
                case "address_bar":
                    return TryNavigateAddressBarStyle(dialogHwnd, normalizedExistingDir);
                case "wps_chain":
                    return TryNavigateWpsCustom(dialogHwnd, normalizedExistingDir);
                case "qt_alt_n":
                {
                    var folderWithSlash = Path.GetFullPath(normalizedExistingDir).TrimEnd('\\', '/') + "\\";
                    ActivateDialog(dialogHwnd);
                    Thread.Sleep(50);
                    TryNavigateQtFileDialog(dialogHwnd, folderWithSlash);
                    return true;
                }
                case "alt_d_value_enter":
                    ActivateDialog(dialogHwnd);
                    Thread.Sleep(100);
                    SendAltD();
                    Thread.Sleep(160);
                    if (TrySetFocusedAddressValue(Path.GetFullPath(normalizedExistingDir)))
                    {
                        Thread.Sleep(50);
                        SendEnter();
                        return true;
                    }

                    return false;
                case "ctrl_l_type_enter":
                    ActivateDialog(dialogHwnd);
                    Thread.Sleep(60);
                    SendCtrlL();
                    Thread.Sleep(120);
                    SendUnicodeString(Path.GetFullPath(normalizedExistingDir));
                    Thread.Sleep(50);
                    SendEnter();
                    return true;
                default:
                    ShellNavigateLog.Write("custom_fd", $"unknown strategy id={strategyId}");
                    return false;
            }
        }
        catch (Exception ex)
        {
            ShellNavigateLog.Write("custom_fd", $"strategy {strategyId}: {ex.Message}");
            return false;
        }
    }
}
