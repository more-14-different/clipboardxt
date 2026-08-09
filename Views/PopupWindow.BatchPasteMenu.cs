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
    private void BatchModeHeader_Click(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        CycleBatchPasteMode();
    }

    private void BatchModeHeader_RightClick(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        OpenBatchMenuPopup();
    }

    private void OpenBatchMenuPopup()
    {
        BatchMenuPopup.PlacementTarget = BatchModeHeaderBorder;
        BatchMenuPopup.Placement = PlacementMode.Bottom;
        RebuildBatchMenuNav();
        _batchNavIndex = 0;
        BatchMenuPopup.IsOpen = true;
        ApplyBatchMenuHighlight();
    }

    private void CloseBatchMenuNavUi()
    {
        foreach (var (row, _) in _batchMenuNav)
            row.ClearValue(Border.BackgroundProperty);
        _batchMenuNav.Clear();
        _batchNavIndex = 0;
    }

    private void RebuildBatchMenuNav()
    {
        _batchMenuNav.Clear();
        void Add(Border b, Action a) => _batchMenuNav.Add((b, a));
        Add(BatchRowPasteAll, ActivateBatchPasteAll);
    }

    private void ApplyBatchMenuHighlight()
    {
        var hi = FindResource("SelectedBrush") as Brush ?? System.Windows.Media.Brushes.LightGray;
        for (int i = 0; i < _batchMenuNav.Count; i++)
            _batchMenuNav[i].Row.Background = i == _batchNavIndex ? hi : System.Windows.Media.Brushes.Transparent;
    }

    private void MoveBatchMenuHighlight(int delta)
    {
        if (_batchMenuNav.Count == 0) return;
        _batchNavIndex = (_batchNavIndex + delta + _batchMenuNav.Count) % _batchMenuNav.Count;
        ApplyBatchMenuHighlight();
    }

    private void ActivateBatchMenuHighlight()
    {
        if (_batchMenuNav.Count == 0) return;
        if (_batchNavIndex < 0 || _batchNavIndex >= _batchMenuNav.Count) return;
        _batchMenuNav[_batchNavIndex].Activate();
    }

    private async void ActivateBatchPasteAll()
    {
        BatchMenuPopup.IsOpen = false;
        CloseBatchMenuNavUi();
        var list = _batchQueue.Snapshot();
        if (list.Count == 0) return;
        ClearSearchText();
#if CLIPX_CLIPBOARD
        var wasFifoOrLifo = GetBatchMode() is BatchPasteQueueMode.Fifo or BatchPasteQueueMode.Lifo;
#endif
        _batchQueue.Clear();
#if CLIPX_CLIPBOARD
        if (_batchQueueProviderSession != null)
        {
            _ = _batchQueueProviderSession.DisposeAsync();
            _batchQueueProviderSession = null;
        }
#endif
        UpdateBatchOrderProperties();
        RefreshFilter(0);
        SyncBatchPasteKeyboardHook();
        var mergeText = _appSettings?.BatchPasteMergeText ?? true;
        if (IsAllTextEntries(list) && mergeText)
            await RunAllTextBatchSingleClipboardAsync(list, newlineAfterEachTextWhenAltEnter: false);
        else if (mergeText)
            await RunOrderedPastesWithAdjacentTextMergeAsync(list, newlineAfterEachTextWhenAltEnter: false);
        else
            await RunSequentialPastesAsync(list);
#if CLIPX_CLIPBOARD
        if (wasFifoOrLifo && list.Count > 0 && (_appSettings?.BatchQueueAutoSwitchToNormalAfterQueueDone ?? true))
            _batchQueueAwaitingNextPasteToSwitchOff = true;
        SyncBatchPasteKeyboardHook();
#endif
    }

    private void BatchMenu_PasteAll_Click(object sender, MouseButtonEventArgs e) => ActivateBatchPasteAll();

    private void HandleMainEnterKey(
        bool newlineAfterEachTextWhenAltEnter = false,
        bool softLineBreakAfterEachTextWhenShiftEnter = false)
    {
        if (ItemsList.SelectedItems.Count > 1)
        {
            if (GetBatchMode() != BatchPasteQueueMode.Off)
            {
                EnqueueSelectedForBatchPasteMode();
                return;
            }
            _ = PasteMultipleSelectedInOrderAsync(
                newlineAfterEachTextWhenAltEnter,
                softLineBreakAfterEachTextWhenShiftEnter);
            return;
        }
        if (GetBatchMode() != BatchPasteQueueMode.Off && _batchQueue.Count > 0)
        {
            _ = PasteBatchQueueHeadAsync();
            return;
        }
        PasteSelectedItem();
    }

    private async Task PasteBatchQueueHeadAsync()
    {
        if (_batchQueue.Count == 0) return;
        if (!_batchQueue.TryAdvance(out var item) || item == null) return;
        ClearSearchText();
        if (!item.IsQuickPaste)
        {
            item.TouchCopiedTime();
            if (item.PersistedId is long pid)
                _historyStore.TryUpdateCopiedAt(pid, item.CopiedAt);
        }
        UpdateBatchOrderProperties();
        ReorderAllItemsQueueFirst();
        RefreshFilter(0);
        try
        {
            await PasteEntryAsync(item, hidePopupAfter: true);
        }
        catch (Exception ex)
        {
            // 恢复队列以防粘贴失败
            _batchQueue.RestoreHead(item);
            UpdateBatchOrderProperties();
            ReorderAllItemsQueueFirst();
            RefreshFilter(0);
            ClipboardDiagnosticsLog.Write($"PasteBatchQueueHeadAsync failed, restored queue: {ex.Message}");
            return;
        }
#if CLIPX_CLIPBOARD
        if (_batchQueue.Count == 0)
            MarkAwaitingBatchQueueNextPasteToSwitchToNormalIfEnabled();
        SyncBatchPasteKeyboardHook();
        if (_batchQueue.Count > 0)
            await TryPushClipboardQueueHeadAsync();
#endif
    }
}
