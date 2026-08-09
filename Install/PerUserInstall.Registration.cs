using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace ClipboardManager;

public static partial class PerUserInstall
{
    /// <summary>若当前正在安装目录下运行，确保卸载注册表项存在。</summary>
    public static void EnsureUninstallRegistrationIfNeeded()
    {
        if (!IsRunningFromInstallLocation()) return;
        try
        {
            using var check = Registry.CurrentUser.OpenSubKey(
                $@"Software\Microsoft\Windows\CurrentVersion\Uninstall\{UninstallRegistryKeyName}");
            if (check == null)
            {
                WriteUninstallRegistry(InstalledExecutablePath);
            }
            else
            {
                try
                {
                    using var key = Registry.CurrentUser.OpenSubKey(
                        $@"Software\Microsoft\Windows\CurrentVersion\Uninstall\{UninstallRegistryKeyName}",
                        writable: true);
                    key?.SetValue("DisplayVersion", AppInfo.DisplayVersion);
                }
                catch
                {
                    // ignore
                }
            }
        }
        catch
        {
            return;
        }

        EnsureStartMenuShortcut(InstalledExecutablePath);
    }

    /// <summary>更新或创建开始菜单中的 ClipboardX 快捷方式（指向已安装的 exe）。</summary>
    public static void EnsureStartMenuShortcut(string targetExePath)
    {
        try
        {
            var folder = Path.GetDirectoryName(StartMenuShortcutPath);
            if (!string.IsNullOrEmpty(folder))
                Directory.CreateDirectory(folder);

            WriteShellShortcut(
                StartMenuShortcutPath,
                targetExePath,
                Path.GetDirectoryName(targetExePath) ?? InstallDirectory,
                DisplayName,
                $"{targetExePath},0");
        }
        catch
        {
            // 无权写开始菜单或 COM 异常时不阻止主流程
        }
    }

    /// <summary>使用 <c>WScript.Shell</c> 写 .lnk，避免 SDK 项目无法引用 <c>IWshRuntimeLibrary</c>。</summary>
    private static void WriteShellShortcut(
        string shortcutPath,
        string targetPath,
        string workingDirectory,
        string description,
        string iconLocation)
    {
        var shellType = Type.GetTypeFromProgID("WScript.Shell");
        if (shellType == null) return;
        object? shell = Activator.CreateInstance(shellType);
        if (shell == null) return;
        object? shortcut = null;
        try
        {
            shortcut = shellType.InvokeMember(
                "CreateShortcut",
                BindingFlags.InvokeMethod,
                null,
                shell,
                [shortcutPath]);
            if (shortcut == null) return;
            var shortcutType = shortcut.GetType();
            shortcutType.InvokeMember("TargetPath", BindingFlags.SetProperty, null, shortcut, [targetPath]);
            shortcutType.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, shortcut, [workingDirectory]);
            shortcutType.InvokeMember("Description", BindingFlags.SetProperty, null, shortcut, [description]);
            shortcutType.InvokeMember("IconLocation", BindingFlags.SetProperty, null, shortcut, [iconLocation]);
            shortcutType.InvokeMember("Save", BindingFlags.InvokeMethod, null, shortcut, null);
        }
        finally
        {
            if (shortcut != null)
                Marshal.FinalReleaseComObject(shortcut);
            Marshal.FinalReleaseComObject(shell);
        }
    }

    private static void TryRemoveStartMenuShortcut()
    {
        try
        {
            if (File.Exists(StartMenuShortcutPath))
                File.Delete(StartMenuShortcutPath);
        }
        catch
        {
            // ignore
        }
    }

    private static void WriteUninstallRegistry(string exePath)
    {
        var installLocation = InstallDirectory;
        var uninstallCommand = $"\"{exePath}\" --uninstall";

        using var key = Registry.CurrentUser.CreateSubKey(
            $@"Software\Microsoft\Windows\CurrentVersion\Uninstall\{UninstallRegistryKeyName}",
            writable: true);
        if (key == null) return;

        key.SetValue("DisplayName", DisplayName);
        key.SetValue("DisplayVersion", AppInfo.DisplayVersion);
        key.SetValue("Publisher", PublisherName);
        key.SetValue("InstallLocation", installLocation);
        key.SetValue("UninstallString", uninstallCommand);
        key.SetValue("QuietUninstallString", uninstallCommand);
        key.SetValue("DisplayIcon", $"\"{exePath}\",0");
        key.SetValue("NoModify", 1, RegistryValueKind.DWord);
        key.SetValue("NoRepair", 1, RegistryValueKind.DWord);

        try
        {
            if (Directory.Exists(installLocation))
            {
                var sizeKb = Directory.EnumerateFiles(installLocation, "*", SearchOption.AllDirectories)
                    .Sum(file => new FileInfo(file).Length) / 1024L;
                if (sizeKb > 0)
                {
                    key.SetValue(
                        "EstimatedSize",
                        Math.Max(1, (int)Math.Min(sizeKb, int.MaxValue)),
                        RegistryValueKind.DWord);
                }
            }
        }
        catch { /* optional */ }
    }
}
