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
    private void LoadHistoryFromStore()
    {
        try
        {
            _historyStore.ArchiveExcess(_maxItems);
            _historyStore.PruneExcessImages(_appSettings?.MaxImageItems ?? 150);
            var batch = _historyStore.LoadNewestFirstLite(_maxItems);
            for (int i = batch.Count - 1; i >= 0; i--)
                _allItems.Insert(0, batch[i]);
        }
        catch { /* ignore */ }
    }

    private void OnImageOcrEntryUpdated()
    {
        RefreshFilter();
        SyncEntryPreviewWithSelection();
    }

    private void LoadQuickPastes()
    {
        _allItems.RemoveAll(x => x.IsQuickPaste);
        foreach (var qp in _quickPastes)
        {
            _allItems.Add(new ClipboardEntry
            {
                Type = EntryType.Text,
                TextContent = qp.Content,
                ShortcutPhrase = qp.Phrase,
                IsQuickPaste = true,
                CopiedAt = DateTime.MinValue
            });
        }
    }

    /// <summary>
    /// 启动时历史已经从 SQLite 装入内存；直接用这份快照构造首次空查询列表，
    /// 避免第一次热键呼出时再次打开数据库并重新水合相同的前 100 条。
    /// </summary>
    private void InitializeFilterFromLoadedItems()
    {
        var dbItems = _allItems
            .Where(item => !item.IsQuickPaste && item.PersistedId.HasValue)
            .OrderByDescending(item => item.IsStarred)
            .ThenByDescending(item => item.CopiedAt)
            .ThenByDescending(item => item.PersistedId)
            .Take(100)
            .ToList();
        ApplyFilterResults("", SearchQuerySpec.Parse(""), dbItems, null);
    }
}
