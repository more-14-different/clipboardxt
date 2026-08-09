using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace ClipboardManager;

public static partial class PerUserInstall
{
    public static bool HasUninstallArgument(string[] args) =>
        args.Any(static argument =>
            string.Equals(argument, "--uninstall", StringComparison.OrdinalIgnoreCase)
            || string.Equals(argument, "/uninstall", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// 从「应用和功能」或带 <c>--uninstall</c> 的进程启动时调用；返回 true 时应退出当前进程（含用户取消）。
    /// </summary>
    public static bool TryProcessUninstallArgs(string[] args)
    {
        if (!HasUninstallArgument(args)) return false;
        RunUninstallWizard(standaloneUninstallProcess: true);
        return true;
    }

    /// <summary>从托盘菜单卸载：用户点「取消」时不退出主程序。</summary>
    public static void PromptUninstallFromTray()
    {
        if (!RunUninstallWizard(standaloneUninstallProcess: false)) return;
        System.Windows.Application.Current.Shutdown();
    }

    /// <returns>是否已执行卸载流程至结束（用户选「取消」时为 false）</returns>
    private static bool RunUninstallWizard(bool standaloneUninstallProcess)
    {
        var version = AppInfo.DisplayVersion;
        var verify = LocalizedMessageBox.Show(
            $"将卸载 ClipboardX（版本 {version}）、移除开始菜单快捷方式、开机启动与「应用和功能」条目，并删除安装目录中的程序文件。\n\n是否同时删除配置与历史记录？（%AppData%\\ClipboardX，旧版可能在 ClipboardManager）\n\n「是」删除程序与配置；「否」只删程序；「取消」中止。",
            $"卸载 ClipboardX — {version}",
            System.Windows.MessageBoxButton.YesNoCancel,
            System.Windows.MessageBoxImage.Question);

        if (verify == System.Windows.MessageBoxResult.Cancel)
        {
            if (standaloneUninstallProcess)
            {
                LocalizedMessageBox.Show(
                    "已取消卸载。",
                    DisplayName,
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
            }
            return false;
        }

        var removeAppData = verify == System.Windows.MessageBoxResult.Yes;

        try
        {
            StartupRegistration.Apply(false, false);
        }
        catch { /* ignore */ }

        try
        {
            using var parent = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Uninstall", writable: true);
            parent?.DeleteSubKeyTree(UninstallRegistryKeyName, throwOnMissingSubKey: false);
        }
        catch { /* ignore */ }

        if (removeAppData)
        {
            try
            {
                if (Directory.Exists(AppPaths.DataRoot))
                    Directory.Delete(AppPaths.DataRoot, recursive: true);
            }
            catch { /* ignore */ }

            foreach (var folder in new[] { "ClipboardX", "ClipboardManager" })
            {
                try
                {
                    var appDataDirectory = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        folder);
                    if (Directory.Exists(appDataDirectory))
                        Directory.Delete(appDataDirectory, recursive: true);
                }
                catch
                {
                    // ignore
                }
            }
        }

        TryRemoveStartMenuShortcut();

        try
        {
            var processStartInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c timeout /t 2 /nobreak >nul && rd /s /q \"{InstallDirectory}\"",
                CreateNoWindow = true,
                UseShellExecute = false,
                WindowStyle = ProcessWindowStyle.Hidden,
            };
            Process.Start(processStartInfo);
        }
        catch
        {
            LocalizedMessageBox.Show(
                "无法启动清理任务。请手动删除文件夹：\n" + InstallDirectory,
                "卸载",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
        }

        LocalizedMessageBox.Show(
            standaloneUninstallProcess
                ? "卸载已完成或即将完成。程序将退出。"
                : "卸载已完成或即将完成。程序即将退出。",
            "卸载",
            System.Windows.MessageBoxButton.OK,
            System.Windows.MessageBoxImage.Information);
        return true;
    }
}
