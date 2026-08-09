using ClipboardManager.Models;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace ClipboardManager;

/// <summary>文件对话框跳转：检索、方向键、主键+数字、收藏，交互对齐主剪贴板面板。</summary>
public partial class FileDialogJumpPickerWindow : Window
{
    public FileDialogJumpPickerWindow(
        IReadOnlyList<FileJumpCandidate> collectorItems,
        int preferSelectedIndex,
        int mouseScreenX,
        int mouseScreenY,
        AppSettings settings,
        IntPtr fileDialogOwnerHwnd,
        bool autoForegroundStickyMode = false,
        IntPtr standaloneTargetHwnd = default,
        Func<string, IntPtr, Task>? standalonePasteCallback = null)
    {
        _fileDialogOwnerHwnd = fileDialogOwnerHwnd;
        _mouseScreenX = mouseScreenX;
        _mouseScreenY = mouseScreenY;
        _settings = settings;
        _autoForegroundStickyMode = autoForegroundStickyMode;
        _isStandaloneMode = standaloneTargetHwnd != IntPtr.Zero;
        _standaloneTargetHwnd = standaloneTargetHwnd;
        _standalonePasteCallback = standalonePasteCallback;
        _ownerFocusHwndBeforePickerActivation = CaptureOwnerFocusBeforePickerActivation(fileDialogOwnerHwnd);
        // 自动弹出模式强制贴对话框；其他模式服从用户的跟随设置。
        _dockBesideDialog = fileDialogOwnerHwnd != IntPtr.Zero
                            && (autoForegroundStickyMode
                                || settings.FileJumpPickerOpenWhenDialogForeground
                                || FileJumpPickerFollowModes.IsDialog(settings.FileJumpPickerFollowMode));
        _collectorSnapshot = collectorItems.ToList();

        InitializeComponent();
        var rememberedState = settings.RememberPanelOperationState
            ? settings.PanelOperationStates.FileJumpPicker
            : null;
        if (rememberedState != null)
        {
            _searchEditor.Restore(
                rememberedState.Text,
                rememberedState.CaretIndex,
                rememberedState.SelectionAnchor);
            _hasSearchText = _searchEditor.HasText;
            if (Enum.IsDefined(typeof(FileJumpPickerFilterMode), rememberedState.FilterMode))
                _filterMode = (FileJumpPickerFilterMode)rememberedState.FilterMode;
        }
        UpdateFilterModeUi();
        Width = settings.FileJumpPickerWidth;
        MaxHeight = settings.FileJumpPickerMaxHeight;
        if (settings.FileJumpPickerHeight > 0)
        {
            _userHasResized = true;
            SizeToContent = SizeToContent.Manual;
            Height = settings.FileJumpPickerHeight;
        }
        Opacity = 0;
        ItemsList.ItemsSource = _displayRows;

        string? preferPath = null;
        if (preferSelectedIndex >= 0 && preferSelectedIndex < _collectorSnapshot.Count)
            preferPath = _collectorSnapshot[preferSelectedIndex].Path;

        BuildMasterList();
        RefreshFilter();
        var restoredPath = rememberedState?.SelectedKey;
        if (!string.IsNullOrEmpty(restoredPath)
            && !_displayRows.Any(row =>
                string.Equals(row.Path, restoredPath, StringComparison.OrdinalIgnoreCase)))
        {
            restoredPath = null;
        }
        var pathToSelect = restoredPath ?? preferPath;
        if (!string.IsNullOrEmpty(pathToSelect))
        {
            var index = _displayRows.ToList().FindIndex(row =>
                string.Equals(row.Path, pathToSelect, StringComparison.OrdinalIgnoreCase));
            if (index >= 0)
            {
                ItemsList.SelectedIndex = index;
                ItemsList.ScrollIntoView(ItemsList.SelectedItem);
            }
        }

        Closed += FileDialogJumpPickerWindow_Closed;
        Activated += FileDialogJumpPickerWindow_Activated;
        SizeChanged += FileDialogJumpPickerWindow_SizeChanged;
        SourceInitialized += FileDialogJumpPickerWindow_SourceInitialized;
        ContentRendered += FileDialogJumpPickerWindow_ContentRendered;
        UpdateSearchChrome();
        UpdateFooterHints();
    }
}
