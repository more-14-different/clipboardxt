using System.Windows;

namespace ClipboardManager;

public partial class App
{
    private void OpenSettings()
    {
        var copy = _settings.ShallowCopy();
        var prevRunAsAdministrator = _settings.RunAsAdministrator;
        var window = new SettingsWindow(copy);
        window.ClearHistoryRequested += () => _popup?.ClearHistory();
        if (_popup != null)
        {
            window.Owner = _popup;
            window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        }
        window.ShowDialog();

        if (window.DialogResult != true) return;

        _settings.ApplyEditableSettingsFrom(copy);
        UiLanguage.Set(_settings.UiLanguage);
        _settings.ApplyRecentFolderLimit();
        SystemClipboardHelper.SetSystemClipboardHistoryEnabled(!_settings.ReplaceSystemWinV);
#if CLIPX_FILEJUMP
        SyncExplorerQuickFindHook();
#endif
        StartupRegistration.Apply(_settings.RunAtStartup, _settings.RunAsAdministrator);
        _settings.Save();
        _popup?.ApplySettings(_settings);
#if CLIPX_CLIPBOARD
        ApplyBatchModeHotkeyAfterSettingsSaved();
#endif
        UpdateTrayTooltip();
        RefreshTrayIcon();
        UiLanguage.ApplyTo(_trayIcon?.ContextMenuStrip);

        if (prevRunAsAdministrator != copy.RunAsAdministrator)
            RestartAfterRunAsAdministratorChange(copy.RunAsAdministrator);
    }

    private void RestartAfterRunAsAdministratorChange(bool wantElevated)
    {
        var args = Environment.GetCommandLineArgs().Skip(1).ToArray();
        var elevated = ProcessElevation.IsCurrentProcessElevated();
        var restarted = wantElevated
            ? elevated
                ? ProcessElevation.TryStartSameExeCopy(args)
                : ProcessElevation.TryStartElevatedCopyAndExit(args)
            : elevated
                ? ProcessElevation.TryStartUnelevatedCopyAndExit(args)
                : ProcessElevation.TryStartSameExeCopy(args);
        if (restarted)
            Shutdown();
    }

#if CLIPX_CLIPBOARD
    private void SetPinyinFilterMode(string mode)
    {
        mode = PinyinFilterModes.Normalize(mode);
        if (_settings.PinyinFilterMode == mode) return;

        _settings.PinyinFilterMode = mode;
        _settings.PinyinFilterIndexVersion = PinyinFilterModes.CurrentIndexVersion;
        _popup?.ApplySettings(_settings);
        _popup?.RebuildPinyinSearchIndex(mode);
        _settings.Save();
    }

    private void EnsureBatchModeHotkeyHost()
    {
        if (_batchModeHotkeyHost != null) return;
        _batchModeHotkeyHost = new BatchModeCycleHotkeyHost();
        _batchModeHotkeyHost.IsForegroundAppExcluded = () => PopupWindow.IsForegroundAppExcluded(_settings);
        _batchModeHotkeyHost.CycleRequested += () =>
            Dispatcher.BeginInvoke(new Action(() => _popup?.CycleBatchPasteMode()));
    }

    /// <summary>设置已写入 _settings；若热键注册失败则回退并再次保存。</summary>
    private void ApplyBatchModeHotkeyAfterSettingsSaved()
    {
        EnsureBatchModeHotkeyHost();
        if (_batchModeHotkeyHost!.TryRegister(_settings.BatchModeCycleHotkeyModifiers, _settings.BatchModeCycleHotkeyKey))
            return;
        LocalizedMessageBox.Show(
            $"批量模式切换快捷键 {_settings.BatchModeCycleHotkeyDisplayName} 注册失败，已恢复原快捷键",
            "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
        _settings.BatchModeCycleHotkeyModifiers = _batchModeHotkeyHost.CurrentModifiers;
        _settings.BatchModeCycleHotkeyKey = _batchModeHotkeyHost.CurrentKey;
        _settings.Save();
    }
#endif

#if CLIPX_FILEJUMP
    /// <summary>按设置安装或卸载资源管理器内 Everything 筛选钩子。</summary>
    private void SyncExplorerQuickFindHook()
    {
        if (!_settings.ExplorerEverythingQuickFindEnabled)
        {
            _explorerQuickFind?.Dispose();
            _explorerQuickFind = null;
            return;
        }

        _explorerQuickFind ??= new ExplorerQuickFindController(Dispatcher, _settings);
        _explorerQuickFind.Start();
    }
#endif
}
