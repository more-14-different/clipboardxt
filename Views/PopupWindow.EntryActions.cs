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
    private void ToggleStarForCurrentSelection()
    {
        var entries = ItemsList.SelectedItems.Cast<ClipboardEntry>()
            .Where(e => _displayItems.Contains(e))
            .ToList();
        if (entries.Count == 0 && ItemsList.SelectedItem is ClipboardEntry selected)
            entries.Add(selected);
        ToggleStarForEntries(entries);
    }

    private void ToggleStarForEntries(IReadOnlyList<ClipboardEntry> entries)
    {
        var targets = entries.Where(e => !e.IsQuickPaste).Distinct().ToList();
        if (targets.Count == 0) return;

        var next = targets.Any(e => !e.IsStarred);
        foreach (var entry in targets)
        {
            entry.IsStarred = next;
            if (entry.IsArchived)
                _historyStore.TryRestoreArchived(entry);
            if (entry.PersistedId is long id)
                _historyStore.TryUpdateStarred(id, next);
        }
        RefreshFilter();
    }

    private void RemoveEntry(ClipboardEntry entry)
    {
        if (ReferenceEquals(_pendingDeleteEntry, entry))
        {
            entry.IsPendingDelete = false;
            _pendingDeleteEntry = null;
        }
        else
            ClearPendingDelete();
        var removedFromPasteQueue = _batchQueue.Remove(entry);
        UpdateBatchOrderProperties();
#if CLIPX_CLIPBOARD
        if (removedFromPasteQueue)
            RequestBatchQueueHeadClipboardResyncAfterDedup();
#endif
        if (entry.IsQuickPaste)
            _quickPastes.RemoveAll(q => q.Content == entry.TextContent);
        else if (entry.IsArchived)
            _historyStore.TryDeleteArchived(entry);
        else
            _historyStore.TryDelete(entry.PersistedId);
        var removedListIndex = _displayItems.IndexOf(entry);
        _allItems.Remove(entry);
        RefreshFilter(removedListIndex >= 0 ? removedListIndex : null);
        if (entry.IsQuickPaste) SaveQuickPastes();
    }

    /// <summary>Del：首次给当前选中项加删除线；同一项再按 Del 才删除。换选或 Esc 取消删除线。</summary>
    private void DeleteSelectedItemWithConfirm()
    {
        if (ItemsList.SelectedItem is not ClipboardEntry entry) return;
        if (entry.IsPendingDelete)
        {
            RemoveEntry(entry);
            return;
        }
        ClearPendingDelete();
        entry.IsPendingDelete = true;
        _pendingDeleteEntry = entry;
    }
}
