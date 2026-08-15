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
    public void TogglePopup()
    {
        var now = Environment.TickCount64;
        if (now - _togglePopupDebounceTick < 45) return;
        _togglePopupDebounceTick = now;
        if (_isPopupVisible) HidePopup(closingViaClipboardHotkey: true);
        else ShowPopup();
    }

    private void ShowPopup()
    {
        _targetWindow = Win32.GetForegroundWindow();
        BeginExternalTextSinkLease();
        BeginFileJumpSearchPasteRoutingIfAvailable();
        var rememberOperationState = _appSettings?.RememberPanelOperationState == true;
        var selectedBeforeRefresh = rememberOperationState
            ? ItemsList.SelectedItem as ClipboardEntry
            : null;
        if (!rememberOperationState)
        {
            ResetSearchEditorState();
            _typeFilter = null;
            _quickPhraseOnly = false;
        }
        _selectionCursor.Reset();
        ClearKeyboardPointSelectionCursor();
        UpdateTypeFilterUi();

        if (!FilterResultsMatchCurrentState())
            RefreshFilter();
        if (selectedBeforeRefresh != null)
        {
            var restoredIndex = _displayItems.IndexOf(selectedBeforeRefresh);
            if (restoredIndex >= 0)
            {
                ItemsList.SelectedIndex = restoredIndex;
                _selectionCursor.SetMouseAnchor(_displayItems.Count, restoredIndex);
                ItemsList.ScrollIntoView(ItemsList.SelectedItem);
            }
        }

        Opacity = 0;
        var caretAutomationProbe = StartCaretAutomationProbeIfUseful();

        _lockPopupWindowNomove = true;
        try
        {
            #region agent log
            AgentDbgLog("H23", "ShowPopup", "phase=pre-show",
                new { _targetWindow = _targetWindow.ToInt64(), ActualHeight, MaxHeight });
            #endregion
            // 先用纯 Win32 快路径给隐藏窗口一个初始坐标；UIA 与首次 WPF 布局并行执行。
            PositionPopup(caretAutomationProbe, allowCaretAutomation: false);
            TryApplyPendingPositionAsWpfLeftTop();
            ApplyPendingPositionSetWindowPos();
            TryApplyPendingPositionAsWpfLeftTop();

            Show();
            UpdateLayout();

            #region agent log
            AgentDbgLog("H23", "ShowPopup", "phase=post-show",
                new { ActualHeight });
            #endregion
            PositionPopup(caretAutomationProbe);

            _isPopupVisible = true;

            ApplyPendingPositionSetWindowPos();
            TryApplyPendingPositionAsWpfLeftTop();
            ReassertPopupTopmostZOrder();
            ApplyShellForegroundZOrderFix();
        }
        finally
        {
            _lockPopupWindowNomove = false;
        }

        // Show()+UpdateLayout 后再做一次定位 + SetWindowPos，确保最终帧停在第二次（caret/UIA 命中）的坐标后才点亮 Opacity，避免「先错位再闪到正确位置」
        Opacity = _popupOpacity;

        if (_displayItems.Count > 0 && ItemsList.SelectedIndex < 0)
            ItemsList.SelectedIndex = 0;

        _awaitHotkeyAltChordCleanup = (_hotkeyModifiers & Win32.MOD_ALT) != 0;
        _hotkeyAltChordCleanupDeadlineTick =
            _awaitHotkeyAltChordCleanup ? Environment.TickCount64 + 750 : 0;

#if CLIPX_CLIPBOARD
        SyncBatchPasteKeyboardHook();
#else
        InstallKeyboardHook();
#endif
        InstallMouseHook();

        // Shell 会在后续帧继续改 Z 序：延迟重申相对置顶（尽力而为）。
        Dispatcher.BeginInvoke(ReassertPopupTopmostZOrder, DispatcherPriority.Loaded);
        Dispatcher.BeginInvoke(() =>
        {
            ReassertPopupTopmostZOrder();
            ApplyShellForegroundZOrderFix();
        }, DispatcherPriority.ContextIdle);

        if (IsShellForegroundWindow(Win32.GetForegroundWindow()))
            ShellForegroundMayOccludePopup?.Invoke();

        _popupPinned = false;
        UpdatePinHeaderUi();
    }

    private void HidePopup(bool closingViaClipboardHotkey = false)
    {
        if (_isResizing) return;
        CancelPendingSearchRefresh();
        EndFileJumpSearchPasteRouting();
        _swallowedMenuAltLatch = false;
        _passthroughModifierLatch = 0;
        _popupPinned = false;
        UpdatePinHeaderUi();
        _isPopupVisible = false;
        _lockPopupWindowNomove = false;
        UninstallMouseHook();
        CloseEntryPreviewBubble();
        CloseContextMenuPopup();
        BatchMenuPopup.IsOpen = false;
        CloseBatchMenuNavUi();
#if CLIPX_CLIPBOARD
        if (GetBatchMode() == BatchPasteQueueMode.Off)
        {
            _batchQueue.Clear();
            if (_batchQueueProviderSession != null)
            {
                _ = _batchQueueProviderSession.DisposeAsync();
                _batchQueueProviderSession = null;
            }
            UpdateBatchOrderProperties();
        }
#else
        _batchQueue.Clear();
        UpdateBatchOrderProperties();
#endif
        _selectionCursor.Reset();
        ShortcutHelpPopup.IsOpen = false;
        PhraseEditPopup.IsOpen = false;
        _phraseEditEntry = null;
        EndPhraseMouseSelection();
        _phraseEditor.Reset();
        TextEntryEditPopup.IsOpen = false;
        _entryTextEditTarget = null;
        _textEditRestoreForegroundHwnd = IntPtr.Zero;
        RestoreNoActivateAfterEntryTextEditIfLifted();
        ClearPendingDelete();
        if (closingViaClipboardHotkey && (_hotkeyModifiers & Win32.MOD_ALT) != 0)
        {
            _awaitHotkeyAltChordCleanup = true;
            _hotkeyAltChordCleanupDeadlineTick = Environment.TickCount64 + 750;
            _ctxAltAwaitRelease = false;
            _ctxAltComboDuringRelease = false;
            _ctxAltCloseMenuArmed = false;
        }
        else
        {
            _awaitHotkeyAltChordCleanup = false;
            _hotkeyAltChordCleanupDeadlineTick = 0;
        }
#if CLIPX_CLIPBOARD
        SyncBatchPasteKeyboardHook();
#else
        if (_awaitHotkeyAltChordCleanup)
            InstallKeyboardHook();
        else
            UninstallKeyboardHook();
#endif
        SavePopupSize();
        Hide();
        EndExternalTextSinkLease();
    }
}
