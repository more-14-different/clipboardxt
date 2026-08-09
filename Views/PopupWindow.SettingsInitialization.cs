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
    public void Initialize(AppSettings settings)
    {
        _appSettings = settings;
        _lastForegroundForDialogTrack = Win32.GetForegroundWindow();
        _maxItems = settings.MaxItems;
        _searchColdArchives = settings.SearchColdArchives;
        _hotkeyModifiers = settings.HotkeyModifiers;
        _hotkeyKey = settings.HotkeyKey;
        _popupPosition = settings.PopupPosition;
        _popupOpacity = settings.PopupOpacity;
        _hideOnSameAppClick = settings.HideOnSameAppClick;
        _panelModifierKey = settings.PanelModifierKey;
        ClipboardEntry.PinyinFilterMode = PinyinFilterModes.Normalize(settings.PinyinFilterMode);
        ClipboardHistoryStore.PinyinFilterMode = ClipboardEntry.PinyinFilterMode;
        ClipboardEntry.PreviewMaxLines = settings.PreviewMaxLines;
        EnsurePinyinSearchIndexVersion(settings);
        _quickPastes = settings.QuickPastes;
        _fileJumpHotkeyModifiers = settings.FileJumpHotkeyModifiers;
        _fileJumpHotkeyKey = settings.FileJumpHotkeyKey;
        ApplyPopupPanelLayout(settings);

#if CLIPX_CLIPBOARD
        LoadHistoryFromStore();
        LoadQuickPastes();
        InitializeFilterFromLoadedItems();
        _ = Task.Run(CleanupOldClipboardExports);
        _imageOcrQueue = new ImageOcrQueue(_historyStore, Dispatcher);
        _imageOcrQueue.EnqueueBackfill(_allItems, settings, OnImageOcrEntryUpdated);
#endif
        UpdateFooterHints();

        Opacity = _popupOpacity;

        var helper = new WindowInteropHelper(this);
        helper.EnsureHandle();
        _hwnd = helper.Handle;

        var exStyle = Win32.GetWindowLongPtr(_hwnd, Win32.GWL_EXSTYLE);
        Win32.SetWindowLongPtr(_hwnd, Win32.GWL_EXSTYLE,
            new IntPtr(exStyle.ToInt64() | Win32.WS_EX_NOACTIVATE | Win32.WS_EX_TOOLWINDOW));

        var source = HwndSource.FromHwnd(_hwnd);
        source?.AddHook(WndProc);

        WarmUpUiaCaretProxy();

#if CLIPX_CLIPBOARD
        if (!Win32.RegisterHotKey(_hwnd, HotkeyId, _hotkeyModifiers | Win32.MOD_NOREPEAT, _hotkeyKey))
        {
            LocalizedMessageBox.Show(
                $"热键 {settings.HotkeyDisplayName} 注册失败，可能被其他程序占用",
                "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
#endif

#if CLIPX_FILEJUMP
        if (!Win32.RegisterHotKey(_hwnd, HotkeyJumpLastFolderId,
                _fileJumpHotkeyModifiers | Win32.MOD_NOREPEAT, _fileJumpHotkeyKey))
        {
            LocalizedMessageBox.Show(
                $"快捷键 {settings.FileJumpHotkeyDisplayName}（文件对话框跳转）注册失败，可能与其他软件冲突",
                "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
#endif

#if CLIPX_CLIPBOARD
        Win32.AddClipboardFormatListener(_hwnd);
#endif
#if CLIPX_FILEJUMP
        InstallForegroundWatcher();
        InstallFileJumpPersistFolderHook();
#endif
        UpdateEmptyState();
        UpdateBatchHeaderUi();
        TextEntryEditPopup.CustomPopupPlacementCallback = TextEntryEditCustomPlacement;

#if CLIPX_CLIPBOARD
        // 根据设置初始化键盘钩子（如果启用了 ReplaceSystemWinV 或其他需要钩子的功能）
        SyncBatchPasteKeyboardHook();
#endif
    }
}
