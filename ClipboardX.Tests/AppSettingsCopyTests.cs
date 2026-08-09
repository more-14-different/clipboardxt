using ClipboardManager;

namespace ClipboardX.Tests;

public sealed class AppSettingsCopyTests
{
    private static readonly string[] DirectEditablePropertyNames =
    [
        nameof(AppSettings.RememberPanelOperationState),
        nameof(AppSettings.MaxItems),
        nameof(AppSettings.SearchColdArchives),
        nameof(AppSettings.HotkeyModifiers),
        nameof(AppSettings.HotkeyKey),
        nameof(AppSettings.FileJumpHotkeyModifiers),
        nameof(AppSettings.FileJumpHotkeyKey),
        nameof(AppSettings.FileJumpPickerShowDelayMs),
        nameof(AppSettings.FileJumpPickerAutoPopup),
        nameof(AppSettings.FileJumpPickerOpenWhenDialogForeground),
        nameof(AppSettings.FileJumpAutoSyncOnReturn),
        nameof(AppSettings.EnableShellNavigateInject),
        nameof(AppSettings.FileJumpAutoOnFirstClick),
        nameof(AppSettings.Theme),
        nameof(AppSettings.PopupPosition),
        nameof(AppSettings.PopupOpacity),
        nameof(AppSettings.HideOnSameAppClick),
        nameof(AppSettings.PasteRequiresDoubleClick),
        nameof(AppSettings.RunAtStartup),
        nameof(AppSettings.RunAsAdministrator),
        nameof(AppSettings.CheckUpdatesOnStartup),
        nameof(AppSettings.ReplaceSystemWinV),
        nameof(AppSettings.ClearHistoryOnExit),
        nameof(AppSettings.PreviewMaxLines),
        nameof(AppSettings.PopupPanelWidth),
        nameof(AppSettings.PopupPanelMaxHeight),
        nameof(AppSettings.PopupPageItems),
        nameof(AppSettings.PanelPageScrollUpModifiers),
        nameof(AppSettings.PanelPageScrollUpKey),
        nameof(AppSettings.PanelPageScrollDownModifiers),
        nameof(AppSettings.PanelPageScrollDownKey),
        nameof(AppSettings.StarToggleHotkeyModifiers),
        nameof(AppSettings.StarToggleHotkeyKey),
        nameof(AppSettings.ClipboardPasteHotkeyModifiers),
        nameof(AppSettings.ClipboardPasteHotkeyKey),
        nameof(AppSettings.ClipboardPasteAsFileHotkeyModifiers),
        nameof(AppSettings.ClipboardPasteAsFileHotkeyKey),
        nameof(AppSettings.ClipboardPasteJsonHotkeyModifiers),
        nameof(AppSettings.ClipboardPasteJsonHotkeyKey),
        nameof(AppSettings.ClipboardEditTextHotkeyModifiers),
        nameof(AppSettings.ClipboardEditTextHotkeyKey),
        nameof(AppSettings.ClipboardShortcutPhraseHotkeyModifiers),
        nameof(AppSettings.ClipboardShortcutPhraseHotkeyKey),
        nameof(AppSettings.ClipboardDeleteHotkeyModifiers),
        nameof(AppSettings.ClipboardDeleteHotkeyKey),
        nameof(AppSettings.FileJumpFavoriteHotkeyModifiers),
        nameof(AppSettings.FileJumpFavoriteHotkeyKey),
        nameof(AppSettings.FileJumpEditPhraseHotkeyModifiers),
        nameof(AppSettings.FileJumpEditPhraseHotkeyKey),
        nameof(AppSettings.FileJumpRemoveRecentHotkeyModifiers),
        nameof(AppSettings.FileJumpRemoveRecentHotkeyKey),
        nameof(AppSettings.PanelModifierKey),
        nameof(AppSettings.BatchModeCycleHotkeyModifiers),
        nameof(AppSettings.BatchModeCycleHotkeyKey),
        nameof(AppSettings.BatchPasteMergeText),
        nameof(AppSettings.BatchQueueAutoSwitchToNormalAfterQueueDone),
        nameof(AppSettings.RecentFolderMaxCount),
        nameof(AppSettings.RecentFolderAutoAddMinCount),
        nameof(AppSettings.ExplorerEverythingQuickFindEnabled),
        nameof(AppSettings.ExplorerEverythingQuickFindMaxResults),
        nameof(AppSettings.FileJumpPickerEverythingFolderSearch),
        nameof(AppSettings.UseFindXSearch),
        nameof(AppSettings.ExplorerQuickFindOpenMode)
    ];

    [Fact]
    public void ApplyEditableSettingsFrom_CopiesEverySettingsWindowValue()
    {
        var target = new AppSettings();
        var source = new AppSettings();

        for (var i = 0; i < DirectEditablePropertyNames.Length; i++)
        {
            var property = typeof(AppSettings).GetProperty(DirectEditablePropertyNames[i])!;
            property.SetValue(target, CreateDistinctValue(property.PropertyType, i, sourceValue: false));
            property.SetValue(source, CreateDistinctValue(property.PropertyType, i, sourceValue: true));
        }

        target.FileJumpPickerFollowMode = FileJumpPickerFollowModes.Dialog;
        source.FileJumpPickerFollowMode = FileJumpPickerFollowModes.Mouse.ToLowerInvariant();
        target.PasteSimulationMode = PasteSimulationModes.CtrlV;
        source.PasteSimulationMode = PasteSimulationModes.ShiftInsert;
        target.ExclusionApps = ["target-app"];
        source.ExclusionApps = ["source-app", "source-app-2"];

        target.ApplyEditableSettingsFrom(source);

        foreach (var propertyName in DirectEditablePropertyNames)
        {
            var property = typeof(AppSettings).GetProperty(propertyName)!;
            Assert.Equal(property.GetValue(source), property.GetValue(target));
        }
        Assert.Equal(FileJumpPickerFollowModes.Mouse, target.FileJumpPickerFollowMode);
        Assert.Equal(PasteSimulationModes.ShiftInsert, target.PasteSimulationMode);
        Assert.Equal(source.ExclusionApps, target.ExclusionApps);
        Assert.NotSame(source.ExclusionApps, target.ExclusionApps);
    }

    [Fact]
    public void ApplyEditableSettingsFrom_PreservesRuntimeOwnedState()
    {
        var quickPastes = new List<QuickPasteEntry> { new() { Phrase = "target", Content = "text" } };
        var favorites = new List<FolderFavoriteEntry> { new() { Phrase = "target", Path = "C:\\target" } };
        var recentFolders = new List<string> { "C:\\target" };
        var confirmCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["C:\\target"] = 3
        };
        var target = new AppSettings
        {
            PinyinFilterMode = PinyinFilterModes.Xiaohe,
            PinyinFilterIndexVersion = 77,
            LastStartupUpdateNotifiedTag = "v-target",
            PopupPanelHeight = 501,
            FileJumpPickerWidth = 502,
            FileJumpPickerMaxHeight = 503,
            FileJumpPickerHeight = 504,
            ExplorerQuickFindWidth = 505,
            ExplorerQuickFindMaxHeight = 506,
            ExplorerQuickFindHeight = 507,
            BatchPasteMode = nameof(BatchPasteQueueMode.Lifo),
            QuickPastes = quickPastes,
            FolderFavorites = favorites,
            LastFileDialogFolder = "C:\\target",
            RecentFileDialogFolders = recentFolders,
            FolderConfirmCounts = confirmCounts
        };
        var source = new AppSettings
        {
            PinyinFilterMode = PinyinFilterModes.Traditional,
            PinyinFilterIndexVersion = 1,
            LastStartupUpdateNotifiedTag = "v-source",
            PopupPanelHeight = 601,
            FileJumpPickerWidth = 602,
            FileJumpPickerMaxHeight = 603,
            FileJumpPickerHeight = 604,
            ExplorerQuickFindWidth = 605,
            ExplorerQuickFindMaxHeight = 606,
            ExplorerQuickFindHeight = 607,
            BatchPasteMode = nameof(BatchPasteQueueMode.Fifo),
            QuickPastes = [new QuickPasteEntry { Phrase = "source", Content = "text" }],
            FolderFavorites = [new FolderFavoriteEntry { Phrase = "source", Path = "C:\\source" }],
            LastFileDialogFolder = "C:\\source",
            RecentFileDialogFolders = ["C:\\source"],
            FolderConfirmCounts = new Dictionary<string, int> { ["C:\\source"] = 9 }
        };

        target.ApplyEditableSettingsFrom(source);

        Assert.Equal(PinyinFilterModes.Xiaohe, target.PinyinFilterMode);
        Assert.Equal(77, target.PinyinFilterIndexVersion);
        Assert.Equal("v-target", target.LastStartupUpdateNotifiedTag);
        Assert.Equal(501, target.PopupPanelHeight);
        Assert.Equal(502, target.FileJumpPickerWidth);
        Assert.Equal(503, target.FileJumpPickerMaxHeight);
        Assert.Equal(504, target.FileJumpPickerHeight);
        Assert.Equal(505, target.ExplorerQuickFindWidth);
        Assert.Equal(506, target.ExplorerQuickFindMaxHeight);
        Assert.Equal(507, target.ExplorerQuickFindHeight);
        Assert.Equal(nameof(BatchPasteQueueMode.Lifo), target.BatchPasteMode);
        Assert.Same(quickPastes, target.QuickPastes);
        Assert.Same(favorites, target.FolderFavorites);
        Assert.Equal("C:\\target", target.LastFileDialogFolder);
        Assert.Same(recentFolders, target.RecentFileDialogFolders);
        Assert.Same(confirmCounts, target.FolderConfirmCounts);
    }

    [Fact]
    public void ApplyEditableSettingsFrom_DisablingOperationStateClearsSessionQueries()
    {
        var target = new AppSettings { RememberPanelOperationState = true };
        target.PanelOperationStates.ExplorerQuickFindQuery = "remembered";
        target.PanelOperationStates.FileJumpPicker = new PanelSearchOperationState(
            "query", 5, -1, 1, "C:\\remembered");
        var source = target.ShallowCopy();
        source.RememberPanelOperationState = false;

        target.ApplyEditableSettingsFrom(source);

        Assert.False(target.RememberPanelOperationState);
        Assert.Equal("", target.PanelOperationStates.ExplorerQuickFindQuery);
        Assert.Null(target.PanelOperationStates.FileJumpPicker);
    }

    private static object CreateDistinctValue(Type type, int index, bool sourceValue)
    {
        var offset = sourceValue ? 1000 : 100;
        if (type == typeof(bool)) return sourceValue;
        if (type == typeof(int)) return offset + index;
        if (type == typeof(uint)) return (uint)(offset + index);
        if (type == typeof(double)) return offset + index + 0.5;
        if (type == typeof(string)) return $"{(sourceValue ? "source" : "target")}-{index}";
        throw new InvalidOperationException($"Unsupported editable setting type: {type}");
    }
}
