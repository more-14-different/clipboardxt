using System.Windows;

namespace ClipboardManager;

public partial class SettingsWindow : Window
{
    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var itemHotkeyConflict = FindItemActionHotkeyConflict();
        if (itemHotkeyConflict != null)
        {
            LocalizedMessageBox.Show(itemHotkeyConflict, "提示",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        string? explorerEverythingMaxResultsText = null;
#if CLIPX_FILEJUMP
        explorerEverythingMaxResultsText = ExplorerEverythingMaxResultsBox.Text;
#endif

        var validation = SettingsSaveValidator.Validate(new SettingsSaveValidationInput(
            MaxItemsBox.Text,
            PreviewLinesBox.Text,
            PopupWidthBox.Text,
            PopupMaxHeightBox.Text,
            PopupPageItemsBox.Text,
            FileJumpDelayMsBox.Text,
            RecentFolderMaxCountBox.Text,
            _pendingModifiers,
            _pendingKey,
            _pendingFileJumpModifiers,
            _pendingFileJumpKey,
            _pendingBatchModeCycleModifiers,
            _pendingBatchModeCycleKey,
            _pendingPageScrollUpModifiers,
            _pendingPageScrollUpKey,
            _pendingPageScrollDownModifiers,
            _pendingPageScrollDownKey,
            explorerEverythingMaxResultsText));

        if (!validation.IsValid || validation.Values == null)
        {
            LocalizedMessageBox.Show(validation.Message ?? "设置无效", "提示",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var values = validation.Values;

        if (!int.TryParse(MaxImageItemsBox.Text, out var maxImageItems) || maxImageItems < 0 || maxImageItems > 5000)
        {
            LocalizedMessageBox.Show("图片历史上限应在 0 ~ 5000 之间", "提示",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _settings.UiLanguage = UiLanguage.Normalize(_pendingUiLanguage);
        _settings.RememberPanelOperationState = _pendingRememberPanelOperationState;
        _settings.MaxItems = values.MaxItems;
        _settings.MaxImageItems = maxImageItems;
        _settings.HotkeyModifiers = _pendingModifiers;
        _settings.HotkeyKey = _pendingKey;
        _settings.FileJumpHotkeyModifiers = _pendingFileJumpModifiers;
        _settings.FileJumpHotkeyKey = _pendingFileJumpKey;
        _settings.BatchModeCycleHotkeyModifiers = _pendingBatchModeCycleModifiers;
        _settings.BatchModeCycleHotkeyKey = _pendingBatchModeCycleKey;
        _settings.FileJumpPickerShowDelayMs = values.FileJumpDelayMs;
        _settings.Theme = _pendingTheme;
        _settings.PopupPosition = _pendingPosition;
        _settings.PopupOpacity = _pendingOpacity;
        _settings.HideOnSameAppClick = _pendingHideOnClick;
        _settings.RunAtStartup = _pendingRunAtStartup;
        _settings.RunAsAdministrator = _pendingRunAsAdministrator;
        _settings.CheckUpdatesOnStartup = _pendingCheckUpdatesOnStartup;
        _settings.ReplaceSystemWinV = _pendingReplaceSystemWinV;
        _settings.ClearHistoryOnExit = _pendingClearHistoryOnExit;
        _settings.SearchColdArchives = _pendingSearchColdArchives;
        _settings.ImageOcrEnabled = _pendingImageOcrEnabled;
        _settings.KeyPassthroughEnabled = _pendingKeyPassthroughEnabled;
        RebuildKeyPassthroughModifierMaskFromChecks();
        _settings.KeyPassthroughModifierMask = _pendingKeyPassthroughModifierMask;
        _settings.KeyPassthroughKeepPanelKeys = KeyPassthroughKeepPanelKeysCheck.IsChecked == true;
        _settings.KeyPassthroughRules = _pendingKeyPassthroughRules
            .Select(r => new KeyPassthroughRule { Modifiers = r.Modifiers, Key = r.Key })
            .ToList();
        _settings.ExclusionApps = _pendingExclusionApps.ToList();
        _settings.EnableShellNavigateInject = _pendingEnableShellNavigateInject;
        _settings.FileJumpPickerFollowMode = FileJumpPickerFollowModes.Normalize(_pendingFileJumpFollowMode);
        _settings.FileJumpPickerOpenWhenDialogForeground = _pendingFileJumpOpenListOnDialogOpen;
        _settings.FileJumpPickerAutoPopup = _pendingFileJumpOpenListOnDialogOpen;
        _settings.FileJumpAutoOnFirstClick = _pendingFileJumpAutoNavigateBest;
        _settings.FileJumpAutoSyncOnReturn = _pendingFileJumpAutoSyncOnReturn;
        _settings.FileJumpPickerEverythingFolderSearch = _pendingFileJumpPickerEverythingFolderSearch;
        _settings.RecentFolderMaxCount = values.RecentFolderMaxCount;
        _settings.ApplyRecentFolderLimit();
#if CLIPX_FILEJUMP
        _settings.ExplorerEverythingQuickFindEnabled = _pendingExplorerEverythingQuickFind;
        _settings.ExplorerEverythingQuickFindMaxResults = values.ExplorerEverythingMaxResults ?? _settings.ExplorerEverythingQuickFindMaxResults;
        _settings.ExplorerQuickFindOpenMode = _pendingExplorerQuickFindOpenMode;
        _settings.UseFindXSearch = false;
#endif
        _settings.PreviewMaxLines = values.PreviewLines;
        _settings.PopupPanelWidth = values.PopupWidth;
        _settings.PopupPanelMaxHeight = values.PopupMaxHeight;
        _settings.PopupPageItems = values.PopupPageItems;
        _settings.PanelPageScrollUpModifiers = _pendingPageScrollUpModifiers;
        _settings.PanelPageScrollUpKey = _pendingPageScrollUpKey;
        _settings.PanelPageScrollDownModifiers = _pendingPageScrollDownModifiers;
        _settings.PanelPageScrollDownKey = _pendingPageScrollDownKey;
        _settings.StarToggleHotkeyModifiers = _pendingStarToggleModifiers;
        _settings.StarToggleHotkeyKey = _pendingStarToggleKey;
        ApplyItemActionHotkeys(_settings);
        AppSettings.NormalizePopupPanelSettings(_settings);
        _settings.PanelModifierKey = _pendingModifierKey;
        _settings.PasteSimulationMode = PasteSimulationModes.Normalize(_pendingPasteSimulationMode);
        _settings.PasteRequiresDoubleClick = _pendingPasteRequiresDoubleClick;
        _settings.BatchPasteMergeText = _pendingBatchPasteMergeText;
        _settings.BatchQueueAutoSwitchToNormalAfterQueueDone = _pendingBatchQueueAutoSwitchToNormalAfterQueueDone;

        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        ThemeManager.Apply(_originalTheme);
        UiLanguage.Set(_originalUiLanguage);
        DialogResult = false;
        Close();
    }

}

