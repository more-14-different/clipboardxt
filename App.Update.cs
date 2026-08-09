using System.IO;
using System.Windows;
using WinForms = System.Windows.Forms;

namespace ClipboardManager;

public partial class App
{
    /// <summary>
    /// 启动约 45 秒后静默请求 GitHub；有新版本时仅托盘气泡提示，同一发行版只提示一次。
    /// </summary>
    private async Task CheckForUpdatesOnStartupAsync()
    {
        if (!_settings.CheckUpdatesOnStartup) return;
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(45)).ConfigureAwait(false);
        }
        catch { /* Task.Delay 取消等 */ }

        if (_trayIcon == null) return;

        GitHubUpdateService.LatestReleaseInfo info;
        try
        {
            info = await GitHubUpdateService.FetchLatestReleaseAsync().ConfigureAwait(false);
        }
        catch
        {
            return;
        }

        var current = AppInfo.DisplayVersion;
        await Dispatcher.InvokeAsync(() =>
        {
            if (_trayIcon == null) return;

            if (!GitHubUpdateService.IsRemoteNewerThanCurrent(info.TagName, current))
            {
                if (!string.IsNullOrEmpty(_settings.LastStartupUpdateNotifiedTag))
                {
                    _settings.LastStartupUpdateNotifiedTag = null;
                    _settings.Save();
                }
                return;
            }

            var tagNorm = info.TagName.Trim();
            if (string.Equals(tagNorm, _settings.LastStartupUpdateNotifiedTag, StringComparison.OrdinalIgnoreCase))
                return;

            _settings.LastStartupUpdateNotifiedTag = tagNorm;
            _settings.Save();
            var ver = tagNorm.TrimStart('v', 'V');
            _trayIcon.ShowBalloonTip(
                12000,
                UiLanguage.T("ClipboardX — 发现新版本"),
                UiLanguage.T($"版本 {ver} 已发布，托盘右键「检查更新…」可下载安装。（当前 {current}）"),
                WinForms.ToolTipIcon.Info);
        });
    }

    private async Task CheckForUpdatesAsync()
    {
        try
        {
            GitHubUpdateService.LatestReleaseInfo info;
            try
            {
                info = await GitHubUpdateService.FetchLatestReleaseAsync().ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                LocalizedMessageBox.Show(
                    $"无法获取更新信息（请检查网络或稍后重试）：\n{ex.Message}",
                    "检查更新",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            var current = AppInfo.DisplayVersion;
            if (!GitHubUpdateService.IsRemoteNewerThanCurrent(info.TagName, current))
            {
                LocalizedMessageBox.Show(
                    $"当前已是最新版本（{current}）。",
                    "检查更新",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            var asset = info.ChosenAsset;
            var note = GitHubUpdateService.TruncateNote(info.Body);
            var verRemote = info.TagName.TrimStart('v', 'V');
            var installDir = PerUserInstall.GetUpdateInstallDirectory();
            var pkgHint = asset.IsNoRuntimeVariant
                ? "已按当前运行方式匹配：**框架依赖**（与本机 dotnet 共享运行时，包较小）。"
                : "已按当前运行方式匹配：**自带运行时**（单文件内含 .NET，包较大）。";
            var prompt =
                $"发现新版本 {verRemote}（当前 {current}）。\n\n" +
                (note.Length > 0 ? $"说明：{note}\n\n" : "") +
                pkgHint + "\n\n" +
                $"将下载：{asset.Name}\n大小约 {GitHubUpdateService.FormatSizeMb(asset.Size)}\n\n" +
                $"安装目录：\n{installDir}\n\n" +
                "程序将关闭后自动替换并重新启动。\n是否继续？";

            if (LocalizedMessageBox.Show(
                    prompt,
                    "ClipboardX 更新",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            var staging = Path.Combine(Path.GetTempPath(), "ClipboardX-update-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(staging);
            var zipPath = Path.Combine(staging, "release.zip");
            var extractDir = Path.Combine(staging, "extract");
            var ps1 = Path.Combine(staging, "apply.ps1");
            var updateLaunched = false;
            try
            {
                await UpdateProgressDialog.RunAsync(
                    "正在从 GitHub 下载更新…",
                    () => GitHubUpdateService.DownloadToFileAsync(asset.DownloadUrl, zipPath));

                GitHubUpdateService.ExtractZipToDirectory(zipPath, extractDir);

                if (!File.Exists(Path.Combine(extractDir, AppInfo.PrimaryExecutableFileName)))
                {
                    LocalizedMessageBox.Show(
                        $"压缩包内未找到 {AppInfo.PrimaryExecutableFileName}，已中止。",
                        "更新",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                if (LocalizedMessageBox.Show(
                        "下载完成。是否立即退出并完成安装？（将自动重启 ClipboardX）",
                        "更新",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question) != MessageBoxResult.Yes)
                    return;

                GitHubUpdateService.LaunchDeferredReplaceAndRestart(extractDir, installDir, staging, ps1,
                    System.Diagnostics.Process.GetCurrentProcess().Id);
                updateLaunched = true;
                Shutdown();
            }
            finally
            {
                if (!updateLaunched && Directory.Exists(staging))
                    GitHubUpdateService.TryDeleteDirectory(staging);
            }
        }
        catch (Exception ex)
        {
            LocalizedMessageBox.Show(
                $"更新未成功：\n{ex.Message}",
                "检查更新",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }
}
