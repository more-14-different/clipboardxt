using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace ClipboardManager;

public partial class SettingsWindow : Window
{
    private readonly AppSettings _settings;
    private readonly string _originalTheme;
    private readonly string _originalUiLanguage;
    private string _pendingUiLanguage;
    private bool _pendingRememberPanelOperationState;
    private uint _pendingModifiers;
    private uint _pendingKey;
    private bool _isRecordingHotkey;
    private uint _pendingFileJumpModifiers;
    private uint _pendingFileJumpKey;
    private bool _isRecordingFileJumpHotkey;
    private uint _pendingBatchModeCycleModifiers;
    private uint _pendingBatchModeCycleKey;
    private bool _isRecordingBatchModeCycleHotkey;
    private string _pendingTheme;
    private string _pendingPosition;
    private double _pendingOpacity;
    private bool _pendingHideOnClick;
    private bool _pendingRunAtStartup;
    private bool _pendingRunAsAdministrator;
    private bool _pendingCheckUpdatesOnStartup;
    private bool _pendingEnableShellNavigateInject;
    private string _pendingFileJumpFollowMode = FileJumpPickerFollowModes.Dialog;
    private bool _pendingFileJumpOpenListOnDialogOpen = true;
    private bool _pendingFileJumpAutoNavigateBest;
    private bool _pendingFileJumpAutoSyncOnReturn;
    private bool _pendingFileJumpPickerEverythingFolderSearch = true;
#if CLIPX_FILEJUMP
    private bool _pendingExplorerEverythingQuickFind;
    private string _pendingExplorerQuickFindOpenMode = "Explorer";
#endif
    private string _pendingModifierKey;
    private bool _pendingBatchPasteMergeText;
    private bool _pendingBatchQueueAutoSwitchToNormalAfterQueueDone;
    private uint _pendingPageScrollUpModifiers;
    private uint _pendingPageScrollUpKey;
    private uint _pendingPageScrollDownModifiers;
    private uint _pendingPageScrollDownKey;
    private bool _isRecordingPageScrollUpHotkey;
    private bool _isRecordingPageScrollDownHotkey;
    private uint _pendingStarToggleModifiers;
    private uint _pendingStarToggleKey;
    private bool _isRecordingStarToggleHotkey;
    private readonly Dictionary<string, (uint Modifiers, uint Key)> _pendingItemActionHotkeys = new();
    private string? _recordingItemActionHotkey;
    private string _pendingPasteSimulationMode = PasteSimulationModes.CtrlV;
    private bool _pendingPasteRequiresDoubleClick;
    private bool _pendingReplaceSystemWinV;
    private bool _pendingClearHistoryOnExit;
    private bool _pendingSearchColdArchives;
    private bool _pendingImageOcrEnabled;
    private bool _pendingKeyPassthroughEnabled;
    private uint _pendingKeyPassthroughModifierMask;
    private bool _pendingKeyPassthroughKeepPanelKeys = true;
    private List<KeyPassthroughRule> _pendingKeyPassthroughRules = new();
    private bool _isRecordingKeyPassthroughRule;
    private List<string> _pendingExclusionApps = new();

    private static readonly string[] ModifierOptions = ["Ctrl", "Alt", "Win", "CapsLock"];

    public event Action? ClearHistoryRequested;

    public SettingsWindow(AppSettings settings)
    {
        InitializeComponent();
        Icon = App.GetWindowIconSource();
        _settings = settings;
        _originalTheme = settings.Theme;
        _originalUiLanguage = UiLanguage.Normalize(settings.UiLanguage);
        _pendingUiLanguage = _originalUiLanguage;
        UpdateUiLanguageText();
        _pendingRememberPanelOperationState = settings.RememberPanelOperationState;
        RememberPanelOperationStateText.Text = _pendingRememberPanelOperationState ? "开启" : "关闭";

#if !CLIPX_CLIPBOARD
        ClipboardTab.Visibility = Visibility.Collapsed;
        ClipboardItemHotkeysSection.Visibility = Visibility.Collapsed;
#endif
#if !CLIPX_FILEJUMP
        FileJumpTab.Visibility = Visibility.Collapsed;
        FileJumpItemHotkeysSection.Visibility = Visibility.Collapsed;
        ExperimentalFeaturesTab.Visibility = Visibility.Collapsed;
        CustomDialogTab.Visibility = Visibility.Collapsed;
#endif

        MaxItemsBox.Text = settings.MaxItems.ToString();
        MaxImageItemsBox.Text = settings.MaxImageItems.ToString();
        _pendingModifiers = settings.HotkeyModifiers;
        _pendingKey = settings.HotkeyKey;
        HotkeyText.Text = settings.HotkeyDisplayName;

        _pendingFileJumpModifiers = settings.FileJumpHotkeyModifiers;
        _pendingFileJumpKey = settings.FileJumpHotkeyKey;
        FileJumpHotkeyText.Text = settings.FileJumpHotkeyDisplayName;

        _pendingBatchModeCycleModifiers = settings.BatchModeCycleHotkeyModifiers;
        _pendingBatchModeCycleKey = settings.BatchModeCycleHotkeyKey;
        BatchModeCycleHotkeyText.Text = settings.BatchModeCycleHotkeyDisplayName;
        InitializeItemActionHotkeys(settings);
        FileJumpDelayMsBox.Text = settings.FileJumpPickerShowDelayMs.ToString();

        _pendingTheme = settings.Theme;
        ThemeText.Text = ThemeDisplayName(_pendingTheme);

        _pendingPosition = settings.PopupPosition;
        PositionText.Text = PositionDisplayName(_pendingPosition);

        _pendingOpacity = settings.PopupOpacity;
        OpacitySlider.Value = _pendingOpacity;
        OpacityValueText.Text = $"{(int)(_pendingOpacity * 100)}%";

        _pendingHideOnClick = settings.HideOnSameAppClick;
        ClickHideText.Text = _pendingHideOnClick ? "任意点击隐藏" : "仅切换应用隐藏";

        _pendingRunAtStartup = settings.RunAtStartup;
        StartupText.Text = _pendingRunAtStartup ? "开启" : "关闭";

        _pendingRunAsAdministrator = settings.RunAsAdministrator;
        RunAsAdminText.Text = _pendingRunAsAdministrator ? "开启" : "关闭";

        _pendingCheckUpdatesOnStartup = settings.CheckUpdatesOnStartup;
        CheckUpdateOnStartupText.Text = _pendingCheckUpdatesOnStartup ? "开启" : "关闭";

        _pendingEnableShellNavigateInject = settings.EnableShellNavigateInject;
        ShellInjectText.Text = _pendingEnableShellNavigateInject ? "开启" : "关闭";

        _pendingFileJumpFollowMode = FileJumpPickerFollowModes.Normalize(settings.FileJumpPickerFollowMode);
        FileJumpFollowText.Text = FileJumpPickerFollowModes.IsDialog(_pendingFileJumpFollowMode) ? "跟随对话框" : "跟随鼠标";

        _pendingFileJumpOpenListOnDialogOpen = settings.FileJumpPickerOpenWhenDialogForeground;
        FileJumpOpenListOnDialogOpenText.Text = _pendingFileJumpOpenListOnDialogOpen ? "开启" : "关闭";

        _pendingFileJumpAutoNavigateBest = settings.FileJumpAutoOnFirstClick;
        FileJumpAutoNavigateBestText.Text = _pendingFileJumpAutoNavigateBest ? "开启" : "关闭";
        UpdateFileJumpFollowVisibility();

        _pendingFileJumpAutoSyncOnReturn = settings.FileJumpAutoSyncOnReturn;
        FileJumpAutoSyncText.Text = _pendingFileJumpAutoSyncOnReturn ? "开启" : "关闭";

        _pendingFileJumpPickerEverythingFolderSearch = settings.FileJumpPickerEverythingFolderSearch;
        FileJumpEverythingFolderText.Text = _pendingFileJumpPickerEverythingFolderSearch ? "开启" : "关闭";

        RecentFolderMaxCountBox.Text = settings.RecentFolderMaxCount.ToString();

#if CLIPX_FILEJUMP
        _pendingExplorerEverythingQuickFind = settings.ExplorerEverythingQuickFindEnabled;
        ExplorerEverythingQuickFindText.Text = _pendingExplorerEverythingQuickFind ? "开启" : "关闭";
        _pendingExplorerQuickFindOpenMode = settings.ExplorerQuickFindOpenMode == "DirectOpen" ? "DirectOpen" : "Explorer";
        ExplorerQuickFindOpenModeText.Text = _pendingExplorerQuickFindOpenMode == "DirectOpen" ? "直接打开" : "从资源管理器打开";
        ExplorerEverythingMaxResultsBox.Text = settings.ExplorerEverythingQuickFindMaxResults.ToString();
#else
        ExplorerEverythingQuickFindText.Text = "—";
        ExplorerEverythingMaxResultsBox.Text = "—";
        ExplorerEverythingQuickFindText.IsEnabled = false;
        ExplorerEverythingMaxResultsBox.IsEnabled = false;
#endif

        PreviewLinesBox.Text = settings.PreviewMaxLines.ToString();

        PopupWidthBox.Text = settings.PopupPanelWidth.ToString("0");
        PopupMaxHeightBox.Text = settings.PopupPanelMaxHeight.ToString("0");
        PopupPageItemsBox.Text = settings.PopupPageItems.ToString();
        _pendingPageScrollUpModifiers = settings.PanelPageScrollUpModifiers;
        _pendingPageScrollUpKey = settings.PanelPageScrollUpKey;
        _pendingPageScrollDownModifiers = settings.PanelPageScrollDownModifiers;
        _pendingPageScrollDownKey = settings.PanelPageScrollDownKey;
        _pendingStarToggleModifiers = settings.StarToggleHotkeyModifiers;
        _pendingStarToggleKey = settings.StarToggleHotkeyKey;
        PanelPageUpKeyText.Text = AppSettings.FormatHotkey(_pendingPageScrollUpModifiers, _pendingPageScrollUpKey);
        PanelPageDownKeyText.Text = AppSettings.FormatHotkey(_pendingPageScrollDownModifiers, _pendingPageScrollDownKey);
        StarToggleHotkeyText.Text = settings.StarToggleHotkeyDisplayName;

        _pendingModifierKey = settings.PanelModifierKey;
        ModifierText.Text = ModifierDisplayName(_pendingModifierKey);

        _pendingPasteSimulationMode = PasteSimulationModes.Normalize(settings.PasteSimulationMode);
        PasteSimulationText.Text = PasteSimulationDisplayName(_pendingPasteSimulationMode);

        _pendingPasteRequiresDoubleClick = settings.PasteRequiresDoubleClick;
        PasteDoubleClickText.Text = _pendingPasteRequiresDoubleClick ? "开启" : "关闭";

        _pendingBatchPasteMergeText = settings.BatchPasteMergeText;
        BatchPasteMergeToggleText.Text = _pendingBatchPasteMergeText ? "开启" : "关闭";

        _pendingBatchQueueAutoSwitchToNormalAfterQueueDone = settings.BatchQueueAutoSwitchToNormalAfterQueueDone;
        BatchQueueAutoNormalToggleText.Text = _pendingBatchQueueAutoSwitchToNormalAfterQueueDone ? "开启" : "关闭";

        _pendingReplaceSystemWinV = settings.ReplaceSystemWinV;
        ReplaceSystemWinVText.Text = _pendingReplaceSystemWinV ? "开启" : "关闭";

        _pendingClearHistoryOnExit = settings.ClearHistoryOnExit;
        ClearHistoryOnExitText.Text = _pendingClearHistoryOnExit ? "开启" : "关闭";

        _pendingSearchColdArchives = settings.SearchColdArchives;
        SearchColdArchivesText.Text = _pendingSearchColdArchives ? "开启" : "关闭";

        _pendingImageOcrEnabled = settings.ImageOcrEnabled;
        ImageOcrEnabledText.Text = _pendingImageOcrEnabled ? "开启" : "关闭";

        _pendingKeyPassthroughEnabled = settings.KeyPassthroughEnabled;
        KeyPassthroughEnabledText.Text = _pendingKeyPassthroughEnabled ? "开启" : "关闭";
        _pendingKeyPassthroughModifierMask = settings.KeyPassthroughModifierMask;
        _pendingKeyPassthroughKeepPanelKeys = settings.KeyPassthroughKeepPanelKeys;
        _pendingKeyPassthroughRules = settings.KeyPassthroughRules
            .Select(r => new KeyPassthroughRule { Modifiers = r.Modifiers, Key = r.Key })
            .ToList();
        ApplyKeyPassthroughModifierChecksFromMask();
        KeyPassthroughKeepPanelKeysCheck.IsChecked = _pendingKeyPassthroughKeepPanelKeys;
        ReloadKeyPassthroughRulesList();

        _pendingExclusionApps = settings.ExclusionApps.ToList();
        ReloadExclusionAppsList();

        CustomFileDialogStore.RulesChanged += OnCustomFileDialogRulesChanged;
        Closed += SettingsWindow_OnClosed;
        Loaded += SettingsWindow_OnLoaded;
        ReloadCustomFileDialogList();
        CustomRulesPathHint.Text = "存储文件：" + CustomFileDialogStore.PersistencePath;
    }

    private void UiLanguageCycle_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        _pendingUiLanguage = UiLanguage.Normalize(_pendingUiLanguage) == UiLanguage.English
            ? UiLanguage.Chinese
            : UiLanguage.English;
        UiLanguage.Set(_pendingUiLanguage);
        UpdateUiLanguageText();
    }

    private void UpdateUiLanguageText()
    {
        UiLanguageText.Text = UiLanguage.Normalize(_pendingUiLanguage) == UiLanguage.English
            ? "英文"
            : "简体中文";
    }

    private void SettingsWindow_OnClosed(object? sender, EventArgs e)
    {
        CustomFileDialogStore.RulesChanged -= OnCustomFileDialogRulesChanged;
    }

    private void SettingsWindow_OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= SettingsWindow_OnLoaded;
        // 托盘/置顶浮层场景下模态窗易落在 Z 序底层；延后到 Input 再夺前台。
        Dispatcher.BeginInvoke(() =>
        {
            Activate();
            try
            {
                var h = new WindowInteropHelper(this).Handle;
                if (h != IntPtr.Zero)
                    Win32.SetForegroundWindowAggressive(h);
            }
            catch { /* ignore */ }
        }, DispatcherPriority.Input);
    }
}
