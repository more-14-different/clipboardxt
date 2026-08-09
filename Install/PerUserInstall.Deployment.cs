using System.Diagnostics;
using System.IO;
using System.Threading;

namespace ClipboardManager;

public static partial class PerUserInstall
{
    /// <summary>
    /// 非安装目录下的发布版 exe：复制到用户 Programs 并启动副本；返回 true 时应退出当前进程。
    /// </summary>
    public static bool TryInstallToUserProgramsAndRelaunch(IReadOnlyList<string> args)
    {
#if DEBUG
        // dotnet run 默认 Debug：apphost 在 bin\...，不应触发安装（否则只拷 exe、安装目录缺 dll 会报错并误退出）
        _ = args.Count;
        return false;
#else
        if (!ShouldUsePerUserInstallPipeline()) return false;
        if (IsRunningFromInstallLocation()) return false;

        try
        {
            TryStopProcessesRunningFromInstallDirectory();

            Directory.CreateDirectory(InstallDirectory);
            var source = Environment.ProcessPath!;
            var sourceDirectory = Path.GetDirectoryName(source)!;

            // 框架依赖：apphost 旁有主 dll，需整套复制；单文件发布仅有大 exe 则无此文件
            var mainDll = Path.ChangeExtension(AppInfo.PrimaryExecutableFileName, ".dll");
            if (File.Exists(Path.Combine(sourceDirectory, mainDll)))
                CopyFrameworkDeploymentFiles(sourceDirectory, InstallDirectory);
            else
                File.Copy(source, InstalledExecutablePath, overwrite: true);

            AppPaths.MergePortableDataDirectoryIntoPerUserLayout(Path.Combine(sourceDirectory, "Data"));
        }
        catch (Exception ex)
        {
            LocalizedMessageBox.Show(
                "未能复制程序到安装目录（可能被杀软拦截、旧进程未退出导致文件被占用、或无权写入）：\n" +
                ex.Message +
                "\n\n可在任务管理器中结束「ClipboardX」后重试，或注销/重启后再试。\n" +
                "将尝试从当前位置继续运行。",
                DisplayName,
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
            return false;
        }

        WriteUninstallRegistry(InstalledExecutablePath);
        EnsureStartMenuShortcut(InstalledExecutablePath);

        try
        {
            var processStartInfo = new ProcessStartInfo
            {
                FileName = InstalledExecutablePath,
                UseShellExecute = false,
            };
            foreach (var argument in args)
                processStartInfo.ArgumentList.Add(argument);
            Process.Start(processStartInfo);
        }
        catch (Exception ex)
        {
            LocalizedMessageBox.Show(
                "已安装到：\n" + InstalledExecutablePath +
                "\n\n但无法启动，请手动运行该路径下程序：\n" + ex.Message,
                DisplayName,
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
            return false;
        }

        return true;
#endif
    }

    private static void TryStopProcessesRunningFromInstallDirectory()
    {
        var installedPath = NormalizePath(InstalledExecutablePath);
        foreach (var process in Process.GetProcessesByName(
                     Path.GetFileNameWithoutExtension(InstalledExecutablePath)))
        {
            try
            {
                string processPath;
                try
                {
                    processPath = process.MainModule?.FileName ?? "";
                }
                catch
                {
                    continue;
                }

                if (!string.Equals(
                        NormalizePath(processPath),
                        installedPath,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                try
                {
                    process.CloseMainWindow();
                }
                catch
                {
                    // 托盘应用常无主窗口
                }

                try
                {
                    if (!process.WaitForExit(2000))
                        process.Kill(entireProcessTree: false);
                }
                catch
                {
                    // 已无权限或已退出
                }
            }
            catch
            {
                // 单进程忽略
            }
            finally
            {
                try
                {
                    process.Dispose();
                }
                catch
                {
                    // ignore
                }
            }
        }

        Thread.Sleep(400);
    }

    /// <summary>将 bin 输出中与运行相关的文件复制到安装目录（不含 .pdb）。</summary>
    private static void CopyFrameworkDeploymentFiles(string sourceDirectory, string destinationDirectory)
    {
        foreach (var path in Directory.EnumerateFiles(sourceDirectory))
        {
            if (!ShouldCopyDeploymentFile(Path.GetFileName(path))) continue;
            var destination = Path.Combine(destinationDirectory, Path.GetFileName(path));
            File.Copy(path, destination, overwrite: true);
        }
    }

    internal static bool ShouldCopyDeploymentFile(string fileName)
    {
        var extension = Path.GetExtension(fileName);
        if (extension.Equals(".pdb", StringComparison.OrdinalIgnoreCase)) return false;
        return extension.Equals(".dll", StringComparison.OrdinalIgnoreCase)
               || extension.Equals(".exe", StringComparison.OrdinalIgnoreCase)
               || extension.Equals(".json", StringComparison.OrdinalIgnoreCase);
    }
}
