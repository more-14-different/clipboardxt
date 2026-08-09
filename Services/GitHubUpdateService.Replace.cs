using System.Diagnostics;
using System.IO;
using System.Text;

namespace ClipboardManager;

internal static partial class GitHubUpdateService
{
    /// <summary>
    /// 写入 ps1 并启动 powershell；主进程应随后立即退出以释放 exe 锁。
    /// <paramref name="cleanupRoot"/> 为临时根目录（内含 extract 子目录等），脚本结束前会尝试删除。
    /// </summary>
    public static void LaunchDeferredReplaceAndRestart(string extractDir, string installDir, string cleanupRoot,
        string scriptPath, int currentPid = 0)
    {
        var extract = EscapeForPowerShellSingleQuoted(Path.GetFullPath(extractDir));
        var install = EscapeForPowerShellSingleQuoted(Path.GetFullPath(installDir));
        var root = EscapeForPowerShellSingleQuoted(Path.GetFullPath(cleanupRoot));
        var exe = EscapeForPowerShellSingleQuoted(
            Path.Combine(Path.GetFullPath(installDir), AppInfo.PrimaryExecutableFileName));

        var sb = new StringBuilder(512);
        sb.AppendLine("$ErrorActionPreference = 'Stop'");
        if (currentPid > 0)
        {
            sb.AppendLine($"$targetPid = {currentPid}");
            sb.AppendLine("$deadline = (Get-Date).AddSeconds(15)");
            sb.AppendLine("while ($true) {");
            sb.AppendLine("  $p = Get-Process -Id $targetPid -ErrorAction SilentlyContinue");
            sb.AppendLine("  if (!$p -or $p.HasExited) { break }");
            sb.AppendLine("  if ((Get-Date) -ge $deadline) {");
            sb.AppendLine("    Stop-Process -Id $targetPid -Force -ErrorAction SilentlyContinue");
            sb.AppendLine("    Start-Sleep -Seconds 1");
            sb.AppendLine("    break");
            sb.AppendLine("  }");
            sb.AppendLine("  Start-Sleep -Milliseconds 250");
            sb.AppendLine("}");
        }
        else
        {
            sb.AppendLine("Start-Sleep -Seconds 3");
        }
        sb.AppendLine($"$src = '{extract}'");
        sb.AppendLine($"$dst = '{install}'");
        sb.AppendLine($"$root = '{root}'");
        sb.AppendLine("Get-ChildItem -LiteralPath $src -Force | ForEach-Object {");
        sb.AppendLine("  $dest = Join-Path $dst $_.Name");
        sb.AppendLine("  Copy-Item -LiteralPath $_.FullName -Destination $dest -Recurse -Force");
        sb.AppendLine("}");
        sb.AppendLine($"Start-Process -FilePath '{exe}'");
        sb.AppendLine("Start-Sleep -Seconds 1");
        sb.AppendLine("Remove-Item -LiteralPath $root -Recurse -Force -ErrorAction SilentlyContinue");
        sb.AppendLine("Remove-Item -LiteralPath $MyInvocation.MyCommand.Path -Force -ErrorAction SilentlyContinue");
        File.WriteAllText(scriptPath, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

        Process.Start(new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"{scriptPath}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
        });
    }

    private static string EscapeForPowerShellSingleQuoted(string s) =>
        s.Replace("'", "''", StringComparison.Ordinal);

    public static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch
        {
            // ignore
        }
    }
}
