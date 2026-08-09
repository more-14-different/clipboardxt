namespace ClipboardManager;

public partial class AppSettings
{
    public AppSettings ShallowCopy() => new()
    {
        UiLanguage = UiLanguage,
        RememberPanelOperationState = RememberPanelOperationState,
        MaxItems = MaxItems,
        MaxImageItems = MaxImageItems,
        MaxImageSizeBytes = MaxImageSizeBytes,
        SearchColdArchives = SearchColdArchives,
        HotkeyModifiers = HotkeyModifiers,
        HotkeyKey = HotkeyKey,
        FileJumpHotkeyModifiers = FileJumpHotkeyModifiers,
        FileJumpHotkeyKey = FileJumpHotkeyKey,
        FileJumpPickerShowDelayMs = FileJumpPickerShowDelayMs,
        FileJumpPickerFollowMode = FileJumpPickerFollowModes.Normalize(FileJumpPickerFollowMode),
        FileJumpPickerAutoPopup = FileJumpPickerAutoPopup,
        FileJumpPickerOpenWhenDialogForeground = FileJumpPickerOpenWhenDialogForeground,
        FileJumpAutoSyncOnReturn = FileJumpAutoSyncOnReturn,
        EnableShellNavigateInject = EnableShellNavigateInject,
        FileJumpAutoOnFirstClick = FileJumpAutoOnFirstClick,
        Theme = Theme,
        PopupPosition = PopupPosition,
        PopupOpacity = PopupOpacity,
        HideOnSameAppClick = HideOnSameAppClick,
        PasteRequiresDoubleClick = PasteRequiresDoubleClick,
        RunAtStartup = RunAtStartup,
        RunAsAdministrator = RunAsAdministrator,
        PasteSimulationMode = PasteSimulationMode,
        CheckUpdatesOnStartup = CheckUpdatesOnStartup,
        ReplaceSystemWinV = ReplaceSystemWinV,
        ClearHistoryOnExit = ClearHistoryOnExit,
        ImageOcrEnabled = ImageOcrEnabled,
        PinyinFilterMode = PinyinFilterModes.Normalize(PinyinFilterMode),
        PinyinFilterIndexVersion = PinyinFilterIndexVersion,
        LastStartupUpdateNotifiedTag = LastStartupUpdateNotifiedTag,
        PreviewMaxLines = PreviewMaxLines,
        PopupPanelWidth = PopupPanelWidth,
        PopupPanelMaxHeight = PopupPanelMaxHeight,
        PopupPanelHeight = PopupPanelHeight,
        FileJumpPickerWidth = FileJumpPickerWidth,
        FileJumpPickerMaxHeight = FileJumpPickerMaxHeight,
        FileJumpPickerHeight = FileJumpPickerHeight,
        ExplorerQuickFindWidth = ExplorerQuickFindWidth,
        ExplorerQuickFindMaxHeight = ExplorerQuickFindMaxHeight,
        ExplorerQuickFindHeight = ExplorerQuickFindHeight,
        PopupPageItems = PopupPageItems,
        PanelPageScrollUpModifiers = PanelPageScrollUpModifiers,
        PanelPageScrollUpKey = PanelPageScrollUpKey,
        PanelPageScrollDownModifiers = PanelPageScrollDownModifiers,
        PanelPageScrollDownKey = PanelPageScrollDownKey,
        StarToggleHotkeyModifiers = StarToggleHotkeyModifiers,
        StarToggleHotkeyKey = StarToggleHotkeyKey,
        ClipboardPasteHotkeyModifiers = ClipboardPasteHotkeyModifiers,
        ClipboardPasteHotkeyKey = ClipboardPasteHotkeyKey,
        ClipboardPasteAsFileHotkeyModifiers = ClipboardPasteAsFileHotkeyModifiers,
        ClipboardPasteAsFileHotkeyKey = ClipboardPasteAsFileHotkeyKey,
        ClipboardPasteJsonHotkeyModifiers = ClipboardPasteJsonHotkeyModifiers,
        ClipboardPasteJsonHotkeyKey = ClipboardPasteJsonHotkeyKey,
        ClipboardEditTextHotkeyModifiers = ClipboardEditTextHotkeyModifiers,
        ClipboardEditTextHotkeyKey = ClipboardEditTextHotkeyKey,
        ClipboardShortcutPhraseHotkeyModifiers = ClipboardShortcutPhraseHotkeyModifiers,
        ClipboardShortcutPhraseHotkeyKey = ClipboardShortcutPhraseHotkeyKey,
        ClipboardDeleteHotkeyModifiers = ClipboardDeleteHotkeyModifiers,
        ClipboardDeleteHotkeyKey = ClipboardDeleteHotkeyKey,
        FileJumpFavoriteHotkeyModifiers = FileJumpFavoriteHotkeyModifiers,
        FileJumpFavoriteHotkeyKey = FileJumpFavoriteHotkeyKey,
        FileJumpEditPhraseHotkeyModifiers = FileJumpEditPhraseHotkeyModifiers,
        FileJumpEditPhraseHotkeyKey = FileJumpEditPhraseHotkeyKey,
        FileJumpRemoveRecentHotkeyModifiers = FileJumpRemoveRecentHotkeyModifiers,
        FileJumpRemoveRecentHotkeyKey = FileJumpRemoveRecentHotkeyKey,
        PanelModifierKey = PanelModifierKey,
        BatchPasteMode = BatchPasteMode,
        BatchModeCycleHotkeyModifiers = BatchModeCycleHotkeyModifiers,
        BatchModeCycleHotkeyKey = BatchModeCycleHotkeyKey,
        BatchPasteMergeText = BatchPasteMergeText,
        BatchQueueAutoSwitchToNormalAfterQueueDone = BatchQueueAutoSwitchToNormalAfterQueueDone,
        QuickPastes = QuickPastes.Select(q => new QuickPasteEntry { Phrase = q.Phrase, Content = q.Content }).ToList(),
        FolderFavorites = FolderFavorites.Select(f => new FolderFavoriteEntry { Phrase = f.Phrase, Path = f.Path }).ToList(),
        LastFileDialogFolder = LastFileDialogFolder,
        RecentFileDialogFolders = RecentFileDialogFolders.ToList(),
        RecentFolderMaxCount = RecentFolderMaxCount,
        RecentFolderAutoAddMinCount = RecentFolderAutoAddMinCount,
        FolderConfirmCounts = FolderConfirmCounts != null
            ? new Dictionary<string, int>(FolderConfirmCounts, StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
        ExplorerEverythingQuickFindEnabled = ExplorerEverythingQuickFindEnabled,
        ExplorerEverythingQuickFindMaxResults = ExplorerEverythingQuickFindMaxResults,
        FileJumpPickerEverythingFolderSearch = FileJumpPickerEverythingFolderSearch,
        UseFindXSearch = UseFindXSearch,
        ExplorerQuickFindOpenMode = ExplorerQuickFindOpenMode,
        ExclusionApps = ExclusionApps.ToList(),
        KeyPassthroughEnabled = KeyPassthroughEnabled,
        KeyPassthroughModifierMask = KeyPassthroughModifierMask,
        KeyPassthroughKeepPanelKeys = KeyPassthroughKeepPanelKeys,
        KeyPassthroughRules = KeyPassthroughRules
            .Select(r => new KeyPassthroughRule { Modifiers = r.Modifiers, Key = r.Key })
            .ToList()
    };

    internal void ApplyEditableSettingsFrom(AppSettings source)
    {
        UiLanguage = ClipboardManager.UiLanguage.Normalize(source.UiLanguage);
        RememberPanelOperationState = source.RememberPanelOperationState;
        if (!RememberPanelOperationState)
            PanelOperationStates.Clear();
        MaxItems = source.MaxItems;
        MaxImageItems = source.MaxImageItems;
        MaxImageSizeBytes = source.MaxImageSizeBytes;
        SearchColdArchives = source.SearchColdArchives;
        HotkeyModifiers = source.HotkeyModifiers;
        HotkeyKey = source.HotkeyKey;
        FileJumpHotkeyModifiers = source.FileJumpHotkeyModifiers;
        FileJumpHotkeyKey = source.FileJumpHotkeyKey;
        FileJumpPickerShowDelayMs = source.FileJumpPickerShowDelayMs;
        FileJumpPickerFollowMode = FileJumpPickerFollowModes.Normalize(source.FileJumpPickerFollowMode);
        FileJumpPickerAutoPopup = source.FileJumpPickerAutoPopup;
        FileJumpPickerOpenWhenDialogForeground = source.FileJumpPickerOpenWhenDialogForeground;
        FileJumpAutoSyncOnReturn = source.FileJumpAutoSyncOnReturn;
        EnableShellNavigateInject = source.EnableShellNavigateInject;
        FileJumpAutoOnFirstClick = source.FileJumpAutoOnFirstClick;
        Theme = source.Theme;
        PopupPosition = source.PopupPosition;
        PopupOpacity = source.PopupOpacity;
        HideOnSameAppClick = source.HideOnSameAppClick;
        PasteRequiresDoubleClick = source.PasteRequiresDoubleClick;
        RunAtStartup = source.RunAtStartup;
        RunAsAdministrator = source.RunAsAdministrator;
        PasteSimulationMode = PasteSimulationModes.Normalize(source.PasteSimulationMode);
        CheckUpdatesOnStartup = source.CheckUpdatesOnStartup;
        ReplaceSystemWinV = source.ReplaceSystemWinV;
        ClearHistoryOnExit = source.ClearHistoryOnExit;
        ImageOcrEnabled = source.ImageOcrEnabled;
        PreviewMaxLines = source.PreviewMaxLines;
        PopupPanelWidth = source.PopupPanelWidth;
        PopupPanelMaxHeight = source.PopupPanelMaxHeight;
        PopupPageItems = source.PopupPageItems;
        PanelPageScrollUpModifiers = source.PanelPageScrollUpModifiers;
        PanelPageScrollUpKey = source.PanelPageScrollUpKey;
        PanelPageScrollDownModifiers = source.PanelPageScrollDownModifiers;
        PanelPageScrollDownKey = source.PanelPageScrollDownKey;
        StarToggleHotkeyModifiers = source.StarToggleHotkeyModifiers;
        StarToggleHotkeyKey = source.StarToggleHotkeyKey;
        ClipboardPasteHotkeyModifiers = source.ClipboardPasteHotkeyModifiers;
        ClipboardPasteHotkeyKey = source.ClipboardPasteHotkeyKey;
        ClipboardPasteAsFileHotkeyModifiers = source.ClipboardPasteAsFileHotkeyModifiers;
        ClipboardPasteAsFileHotkeyKey = source.ClipboardPasteAsFileHotkeyKey;
        ClipboardPasteJsonHotkeyModifiers = source.ClipboardPasteJsonHotkeyModifiers;
        ClipboardPasteJsonHotkeyKey = source.ClipboardPasteJsonHotkeyKey;
        ClipboardEditTextHotkeyModifiers = source.ClipboardEditTextHotkeyModifiers;
        ClipboardEditTextHotkeyKey = source.ClipboardEditTextHotkeyKey;
        ClipboardShortcutPhraseHotkeyModifiers = source.ClipboardShortcutPhraseHotkeyModifiers;
        ClipboardShortcutPhraseHotkeyKey = source.ClipboardShortcutPhraseHotkeyKey;
        ClipboardDeleteHotkeyModifiers = source.ClipboardDeleteHotkeyModifiers;
        ClipboardDeleteHotkeyKey = source.ClipboardDeleteHotkeyKey;
        FileJumpFavoriteHotkeyModifiers = source.FileJumpFavoriteHotkeyModifiers;
        FileJumpFavoriteHotkeyKey = source.FileJumpFavoriteHotkeyKey;
        FileJumpEditPhraseHotkeyModifiers = source.FileJumpEditPhraseHotkeyModifiers;
        FileJumpEditPhraseHotkeyKey = source.FileJumpEditPhraseHotkeyKey;
        FileJumpRemoveRecentHotkeyModifiers = source.FileJumpRemoveRecentHotkeyModifiers;
        FileJumpRemoveRecentHotkeyKey = source.FileJumpRemoveRecentHotkeyKey;
        PanelModifierKey = source.PanelModifierKey;
        BatchModeCycleHotkeyModifiers = source.BatchModeCycleHotkeyModifiers;
        BatchModeCycleHotkeyKey = source.BatchModeCycleHotkeyKey;
        BatchPasteMergeText = source.BatchPasteMergeText;
        BatchQueueAutoSwitchToNormalAfterQueueDone = source.BatchQueueAutoSwitchToNormalAfterQueueDone;
        RecentFolderMaxCount = source.RecentFolderMaxCount;
        RecentFolderAutoAddMinCount = source.RecentFolderAutoAddMinCount;
        ExplorerEverythingQuickFindEnabled = source.ExplorerEverythingQuickFindEnabled;
        ExplorerEverythingQuickFindMaxResults = source.ExplorerEverythingQuickFindMaxResults;
        FileJumpPickerEverythingFolderSearch = source.FileJumpPickerEverythingFolderSearch;
        UseFindXSearch = source.UseFindXSearch;
        ExplorerQuickFindOpenMode = source.ExplorerQuickFindOpenMode;
        ExclusionApps = source.ExclusionApps.ToList();
        KeyPassthroughEnabled = source.KeyPassthroughEnabled;
        KeyPassthroughModifierMask = source.KeyPassthroughModifierMask;
        KeyPassthroughKeepPanelKeys = source.KeyPassthroughKeepPanelKeys;
        KeyPassthroughRules = source.KeyPassthroughRules
            .Select(r => new KeyPassthroughRule { Modifiers = r.Modifiers, Key = r.Key })
            .ToList();
    }
}

