using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Brush = System.Windows.Media.Brush;
using Button = System.Windows.Controls.Button;
using Orientation = System.Windows.Controls.Orientation;
using TextBox = System.Windows.Controls.TextBox;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using ClipboardManager.Models;

namespace ClipboardManager;

public partial class PopupWindow : Window
{
    private void ApplyPopupPanelLayout(AppSettings settings)
    {
        AppSettings.NormalizePopupPanelSettings(settings);
        _pageSize = settings.PopupPageItems;
        _panelPageScrollUpModifiers = settings.PanelPageScrollUpModifiers;
        _panelPageScrollUpKey = settings.PanelPageScrollUpKey;
        _panelPageScrollDownModifiers = settings.PanelPageScrollDownModifiers;
        _panelPageScrollDownKey = settings.PanelPageScrollDownKey;
        _starToggleHotkeyModifiers = settings.StarToggleHotkeyModifiers;
        _starToggleHotkeyKey = settings.StarToggleHotkeyKey;
        Width = settings.PopupPanelWidth;
        MaxHeight = settings.PopupPanelMaxHeight;
        if (settings.PopupPanelHeight > 0)
        {
            _userHasResized = true;
            SizeToContent = SizeToContent.Manual;
            Height = settings.PopupPanelHeight;
        }
    }

    public void ApplySettings(AppSettings settings)
    {
        var searchColdArchivesChanged = _searchColdArchives != settings.SearchColdArchives;
        var clearOperationState = !settings.RememberPanelOperationState;
        _appSettings = settings;
        _maxItems = settings.MaxItems;
        _searchColdArchives = settings.SearchColdArchives;
        _popupPosition = settings.PopupPosition;
        _popupOpacity = settings.PopupOpacity;
        _hideOnSameAppClick = settings.HideOnSameAppClick;
        _panelModifierKey = settings.PanelModifierKey;
        ClipboardEntry.PinyinFilterMode = PinyinFilterModes.Normalize(settings.PinyinFilterMode);
        ClipboardHistoryStore.PinyinFilterMode = ClipboardEntry.PinyinFilterMode;
        ClipboardEntry.PreviewMaxLines = settings.PreviewMaxLines;
        EnsurePinyinSearchIndexVersion(settings);
        Opacity = _popupOpacity;
        _quickPastes = settings.QuickPastes;
        ApplyPopupPanelLayout(settings);
        TrimItems();
        UpdateBatchHeaderUi();

        if (!settings.KeyPassthroughEnabled)
            _passthroughModifierLatch = 0;

#if CLIPX_CLIPBOARD
        if (settings.ImageOcrEnabled && _imageOcrQueue != null)
            _imageOcrQueue.EnqueueBackfill(_allItems, settings, OnImageOcrEntryUpdated);
#endif

        if (!settings.FileJumpAutoOnFirstClick)
        {
            _fileJumpAutoFirstJumpDoneRoot = IntPtr.Zero;
            DisarmFileJumpClickToNavigate();
        }

#if CLIPX_CLIPBOARD
        if (settings.HotkeyModifiers != _hotkeyModifiers || settings.HotkeyKey != _hotkeyKey)
        {
            if (!UpdateHotkey(settings.HotkeyModifiers, settings.HotkeyKey))
            {
                LocalizedMessageBox.Show(
                    $"热键 {settings.HotkeyDisplayName} 注册失败，已恢复原快捷键",
                    "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                settings.HotkeyModifiers = _hotkeyModifiers;
                settings.HotkeyKey = _hotkeyKey;
            }
        }
#endif

        if (settings.FileJumpHotkeyModifiers != _fileJumpHotkeyModifiers
            || settings.FileJumpHotkeyKey != _fileJumpHotkeyKey)
        {
            if (!UpdateFileJumpHotkey(settings.FileJumpHotkeyModifiers, settings.FileJumpHotkeyKey))
            {
                LocalizedMessageBox.Show(
                    $"文件对话框跳转快捷键 {settings.FileJumpHotkeyDisplayName} 注册失败，已恢复原快捷键",
                    "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                settings.FileJumpHotkeyModifiers = _fileJumpHotkeyModifiers;
                settings.FileJumpHotkeyKey = _fileJumpHotkeyKey;
            }
        }

        UpdateFooterHints();

        if (clearOperationState)
        {
            ResetSearchEditorState();
            _typeFilter = null;
            _quickPhraseOnly = false;
            UpdateTypeFilterUi();
            if (_isPopupVisible)
                RefreshFilter();
            else
                UpdateSearchUI();
        }

        if (searchColdArchivesChanged && !clearOperationState)
        {
            CancelPendingSearchRefresh();
            if (_isPopupVisible)
                RefreshFilter();
        }

#if CLIPX_CLIPBOARD
        // 更新键盘钩子状态（可能因为 ReplaceSystemWinV 设置变化需要激活/停用钩子）
        SyncBatchPasteKeyboardHook();
#endif
    }
}
