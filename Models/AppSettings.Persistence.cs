using System.IO;
using System.Text.Json;

namespace ClipboardManager;

public partial class AppSettings
{    private static readonly string LegacySettingsDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ClipboardManager");

    private static string SettingsDir => Path.GetDirectoryName(AppPaths.SettingsFile)!;

    private static string SettingsFile => AppPaths.SettingsFile;
    private static readonly string LegacySettingsFile = Path.Combine(LegacySettingsDir, "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsFile))
            {
                var json = File.ReadAllText(SettingsFile);
                using (var doc = JsonDocument.Parse(json))
                {
                    var settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new();
                    settings.UiLanguage = ClipboardManager.UiLanguage.Normalize(settings.UiLanguage);
                    if (!doc.RootElement.TryGetProperty(nameof(RememberPanelOperationState), out _))
                        settings.RememberPanelOperationState = true;
                    // 旧版 settings.json 无此字段时 Json 反序列化为 false，产品默认应为开启
                    if (!doc.RootElement.TryGetProperty(nameof(RunAtStartup), out _))
                        settings.RunAtStartup = true;
                    if (!doc.RootElement.TryGetProperty(nameof(RunAsAdministrator), out _))
                        settings.RunAsAdministrator = true;
                    if (!doc.RootElement.TryGetProperty(nameof(EnableShellNavigateInject), out _))
                        settings.EnableShellNavigateInject = true;
                    if (!doc.RootElement.TryGetProperty(nameof(FileJumpAutoOnFirstClick), out _))
                        settings.FileJumpAutoOnFirstClick = false;
                    if (!doc.RootElement.TryGetProperty(nameof(FileJumpPickerFollowMode), out _))
                    {
                        if (doc.RootElement.TryGetProperty("FileJumpPickerDockBesideDialog", out var dockEl)
                            && dockEl.ValueKind == JsonValueKind.False)
                            settings.FileJumpPickerFollowMode = FileJumpPickerFollowModes.Mouse;
                        else
                            settings.FileJumpPickerFollowMode = FileJumpPickerFollowModes.Dialog;
                    }
                    settings.FileJumpPickerFollowMode = FileJumpPickerFollowModes.Normalize(settings.FileJumpPickerFollowMode);
                    if (!doc.RootElement.TryGetProperty(nameof(FileJumpPickerAutoPopup), out _))
                        settings.FileJumpPickerAutoPopup = true;
                    if (!doc.RootElement.TryGetProperty(nameof(FileJumpPickerOpenWhenDialogForeground), out _))
                        settings.FileJumpPickerOpenWhenDialogForeground = true;
                    NormalizeFileJumpDialogAutoOpen(settings);
                    if (!doc.RootElement.TryGetProperty(nameof(FileJumpAutoSyncOnReturn), out _))
                        settings.FileJumpAutoSyncOnReturn = true;
                    if (!doc.RootElement.TryGetProperty(nameof(CheckUpdatesOnStartup), out _))
                        settings.CheckUpdatesOnStartup = true;
                    if (!doc.RootElement.TryGetProperty(nameof(BatchPasteMergeText), out _))
                        settings.BatchPasteMergeText = true;
                    if (!doc.RootElement.TryGetProperty(nameof(BatchQueueAutoSwitchToNormalAfterQueueDone), out _))
                    {
                        if (doc.RootElement.TryGetProperty("FifoAutoSwitchToNormalAfterQueueDone", out var legacyFifo))
                            settings.BatchQueueAutoSwitchToNormalAfterQueueDone = legacyFifo.ValueKind == JsonValueKind.True;
                        else
                            settings.BatchQueueAutoSwitchToNormalAfterQueueDone = true;
                    }
                    if (!doc.RootElement.TryGetProperty(nameof(ExplorerEverythingQuickFindMaxResults), out _))
                        settings.ExplorerEverythingQuickFindMaxResults = 150;
                    if (!doc.RootElement.TryGetProperty(nameof(ExplorerEverythingQuickFindEnabled), out _))
                        settings.ExplorerEverythingQuickFindEnabled = true;
                    if (!doc.RootElement.TryGetProperty(nameof(FileJumpPickerEverythingFolderSearch), out _))
                        settings.FileJumpPickerEverythingFolderSearch = true;
                    if (!doc.RootElement.TryGetProperty(nameof(RecentFolderMaxCount), out _))
                        settings.RecentFolderMaxCount = 50;
                    if (!doc.RootElement.TryGetProperty(nameof(RecentFolderAutoAddMinCount), out _))
                        settings.RecentFolderAutoAddMinCount = 1;
                    if (settings.FolderFavorites == null)
                        settings.FolderFavorites = new List<FolderFavoriteEntry>();
                    if (settings.FolderConfirmCounts == null)
                        settings.FolderConfirmCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                    MigrateRecentFileDialogFolders(settings);
                    settings.PasteSimulationMode = PasteSimulationModes.Normalize(settings.PasteSimulationMode);
                    settings.PinyinFilterMode = PinyinFilterModes.Normalize(settings.PinyinFilterMode);
                    if (!doc.RootElement.TryGetProperty(nameof(PinyinFilterIndexVersion), out _))
                        settings.PinyinFilterIndexVersion = 0;
                    NormalizePopupPanelSettings(settings);
                    return settings;
                }
            }

            if (File.Exists(LegacySettingsFile))
            {
                var json = File.ReadAllText(LegacySettingsFile);
                using (var doc = JsonDocument.Parse(json))
                {
                    var settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new();
                    if (!doc.RootElement.TryGetProperty(nameof(RememberPanelOperationState), out _))
                        settings.RememberPanelOperationState = true;
                    if (!doc.RootElement.TryGetProperty(nameof(RunAtStartup), out _))
                        settings.RunAtStartup = true;
                    if (!doc.RootElement.TryGetProperty(nameof(RunAsAdministrator), out _))
                        settings.RunAsAdministrator = true;
                    if (!doc.RootElement.TryGetProperty(nameof(EnableShellNavigateInject), out _))
                        settings.EnableShellNavigateInject = true;
                    if (!doc.RootElement.TryGetProperty(nameof(FileJumpAutoOnFirstClick), out _))
                        settings.FileJumpAutoOnFirstClick = false;
                    if (!doc.RootElement.TryGetProperty(nameof(FileJumpPickerFollowMode), out _))
                    {
                        if (doc.RootElement.TryGetProperty("FileJumpPickerDockBesideDialog", out var dockEl)
                            && dockEl.ValueKind == JsonValueKind.False)
                            settings.FileJumpPickerFollowMode = FileJumpPickerFollowModes.Mouse;
                        else
                            settings.FileJumpPickerFollowMode = FileJumpPickerFollowModes.Dialog;
                    }
                    settings.FileJumpPickerFollowMode = FileJumpPickerFollowModes.Normalize(settings.FileJumpPickerFollowMode);
                    if (!doc.RootElement.TryGetProperty(nameof(FileJumpPickerAutoPopup), out _))
                        settings.FileJumpPickerAutoPopup = true;
                    if (!doc.RootElement.TryGetProperty(nameof(FileJumpPickerOpenWhenDialogForeground), out _))
                        settings.FileJumpPickerOpenWhenDialogForeground = true;
                    NormalizeFileJumpDialogAutoOpen(settings);
                    if (!doc.RootElement.TryGetProperty(nameof(FileJumpAutoSyncOnReturn), out _))
                        settings.FileJumpAutoSyncOnReturn = true;
                    if (!doc.RootElement.TryGetProperty(nameof(CheckUpdatesOnStartup), out _))
                        settings.CheckUpdatesOnStartup = true;
                    if (!doc.RootElement.TryGetProperty(nameof(BatchPasteMergeText), out _))
                        settings.BatchPasteMergeText = true;
                    if (!doc.RootElement.TryGetProperty(nameof(BatchQueueAutoSwitchToNormalAfterQueueDone), out _))
                    {
                        if (doc.RootElement.TryGetProperty("FifoAutoSwitchToNormalAfterQueueDone", out var legacyFifo))
                            settings.BatchQueueAutoSwitchToNormalAfterQueueDone = legacyFifo.ValueKind == JsonValueKind.True;
                        else
                            settings.BatchQueueAutoSwitchToNormalAfterQueueDone = true;
                    }
                    if (!doc.RootElement.TryGetProperty(nameof(ExplorerEverythingQuickFindMaxResults), out _))
                        settings.ExplorerEverythingQuickFindMaxResults = 150;
                    if (!doc.RootElement.TryGetProperty(nameof(ExplorerEverythingQuickFindEnabled), out _))
                        settings.ExplorerEverythingQuickFindEnabled = true;
                    if (!doc.RootElement.TryGetProperty(nameof(FileJumpPickerEverythingFolderSearch), out _))
                        settings.FileJumpPickerEverythingFolderSearch = true;
                    if (!doc.RootElement.TryGetProperty(nameof(RecentFolderMaxCount), out _))
                        settings.RecentFolderMaxCount = 50;
                    if (!doc.RootElement.TryGetProperty(nameof(RecentFolderAutoAddMinCount), out _))
                        settings.RecentFolderAutoAddMinCount = 1;
                    if (settings.FolderFavorites == null)
                        settings.FolderFavorites = new List<FolderFavoriteEntry>();
                    if (settings.FolderConfirmCounts == null)
                        settings.FolderConfirmCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                    MigrateRecentFileDialogFolders(settings);
                    settings.PasteSimulationMode = PasteSimulationModes.Normalize(settings.PasteSimulationMode);
                    settings.PinyinFilterMode = PinyinFilterModes.Normalize(settings.PinyinFilterMode);
                    if (!doc.RootElement.TryGetProperty(nameof(PinyinFilterIndexVersion), out _))
                        settings.PinyinFilterIndexVersion = 0;
                    NormalizePopupPanelSettings(settings);
                    settings.SaveSync();
                    return settings;
                }
            }
        }
        catch { }
        return new();
    }

    /// <summary>
    /// 将历史字段 <see cref="FileJumpPickerAutoPopup"/> 与 <see cref="FileJumpPickerOpenWhenDialogForeground"/> 拉齐：
    /// 当前版本两者同义（均代表"对话框打开时自动弹出列表"），任一为 true 即两者都置 true，避免历史 JSON 留下不一致。
    /// </summary>
    private static void NormalizeFileJumpDialogAutoOpen(AppSettings settings)
    {
        var openList = settings.FileJumpPickerOpenWhenDialogForeground || settings.FileJumpPickerAutoPopup;
        settings.FileJumpPickerOpenWhenDialogForeground = openList;
        settings.FileJumpPickerAutoPopup = openList;
    }

    private static void MigrateRecentFileDialogFolders(AppSettings settings)
    {
        settings.RecentFileDialogFolders ??= new List<string>();
        settings.ExclusionApps ??= new List<string>();
        settings.RecentFileDialogFolders.RemoveAll(string.IsNullOrWhiteSpace);
        if (settings.RecentFileDialogFolders.Count == 0 && !string.IsNullOrWhiteSpace(settings.LastFileDialogFolder))
        {
            try
            {
                var n = Path.GetFullPath(settings.LastFileDialogFolder.Trim());
                if (Directory.Exists(n))
                    settings.RecentFileDialogFolders.Add(n);
            }
            catch { /* ignore */ }
        }

        if (settings.RecentFileDialogFolders.Count > 0)
            settings.LastFileDialogFolder = settings.RecentFileDialogFolders[0].Trim();
        settings.ApplyRecentFolderLimit();
    }
}

