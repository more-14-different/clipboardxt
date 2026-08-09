using System.IO;

namespace ClipboardManager;

/// <summary>
/// 将程序安装到当前用户目录（%LocalAppData%\Programs\ClipboardX），并注册“应用和功能”卸载项。
/// </summary>
public static partial class PerUserInstall
{
    public const string UninstallRegistryKeyName = "ClipboardX";
    private const string PublisherName = "ClipboardX";

    private static readonly string InstallRootRelative =
        Path.Combine("Programs", "ClipboardX");

    public static string InstallDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            InstallRootRelative);

    public static string InstalledExecutablePath =>
        Path.Combine(InstallDirectory, AppInfo.PrimaryExecutableFileName);

    public static string DisplayName => "ClipboardX";

    /// <summary>当前用户「开始」菜单程序文件夹中的快捷方式路径。</summary>
    public static string StartMenuShortcutPath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            @"Microsoft\Windows\Start Menu\Programs",
            $"{DisplayName}.lnk");

    private static bool IsDevBypass =>
        string.Equals(Environment.GetEnvironmentVariable("CLIPBOARD_MANAGER_DEV"),
            "1", StringComparison.Ordinal);

    /// <summary>
    /// 是否允许执行“复制到用户 Program 目录”（正式分发用的 exe）；
    /// <c>dotnet.exe</c> 直接托管 dll 时 ProcessPath 为 dotnet，会跳过。
    /// </summary>
    /// <remarks>
    /// <c>dotnet run</c> 实际会启动 <c>bin\...\ClipboardX.exe</c> apphost，ProcessPath 不是 dotnet；
    /// 若仅复制 exe 而安装目录无同名 dll，框架依赖部署会启动失败，故 Debug 构建或需整套文件复制时另有处理。
    /// </remarks>
    public static bool ShouldUsePerUserInstallPipeline()
    {
        if (IsDevBypass) return false;
        var path = Environment.ProcessPath;
        if (string.IsNullOrEmpty(path)) return false;
        if (path.EndsWith("dotnet.exe", StringComparison.OrdinalIgnoreCase)) return false;
        return path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && File.Exists(path);
    }

    private static string NormalizePath(string path)
    {
        try
        {
            return Path.GetFullPath(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        }
        catch
        {
            return path;
        }
    }

    public static bool IsRunningFromInstallLocation()
    {
        if (!ShouldUsePerUserInstallPipeline()) return false;
        var current = NormalizePath(Environment.ProcessPath!);
        var installed = NormalizePath(InstalledExecutablePath);
        return string.Equals(current, installed, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>在线更新时覆盖此目录（与 PerUserInstall 安装目录一致，或为当前便携运行目录）。</summary>
    public static string GetUpdateInstallDirectory()
    {
        if (AppPaths.IsPortable)
        {
            var dir = Path.GetDirectoryName(Environment.ProcessPath);
            return string.IsNullOrEmpty(dir) ? AppContext.BaseDirectory : NormalizePath(dir);
        }
        if (IsRunningFromInstallLocation())
            return InstallDirectory;
        var currentDirectory = Path.GetDirectoryName(Environment.ProcessPath);
        return string.IsNullOrEmpty(currentDirectory) ? InstallDirectory : NormalizePath(currentDirectory);
    }
}
