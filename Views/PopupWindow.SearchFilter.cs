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
    private bool FilterResultsMatchCurrentState() =>
        _hasAppliedFilterResults
        && string.Equals(_lastAppliedFilterQuery, _searchText, StringComparison.Ordinal)
        && _lastAppliedFilterType == _typeFilter
        && _lastAppliedQuickPhraseOnly == _quickPhraseOnly
        && _lastAppliedSearchColdArchives == _searchColdArchives;

    /// <param name="preferSelectListIndex">
    /// 刷新后希望选中的行（0-based，对应当前列表）。删除条目时传「被删项在原列表中的索引」，则选中同一位置（由下一项顶替），删最后一项则选中新末项。
    /// 其他场景省略或传 null，默认选中第 0 条。
    /// </param>
    private void RefreshFilter(int? preferSelectListIndex = null)
    {
        CancelPendingSearchRefresh();
        _searchRefreshGeneration++;
        var query = _searchText;
        var spec = SearchQuerySpec.Parse(query);
        var dbItems = _historyStore.Search(
            query,
            _typeFilter,
            100,
            _quickPhraseOnly,
            _searchColdArchives);
        ApplyFilterResults(query, spec, dbItems, preferSelectListIndex);
    }

    private async void RefreshFilterForSearchInput()
    {
        CancelPendingSearchRefresh();
        var cancellation = new CancellationTokenSource();
        _searchRefreshCancellation = cancellation;
        var generation = ++_searchRefreshGeneration;
        var query = _searchText;
        var typeFilter = _typeFilter;
        var quickPhraseOnly = _quickPhraseOnly;
        var searchColdArchives = _searchColdArchives;
        var spec = SearchQuerySpec.Parse(query);

        // 搜索文字立即反馈；数据库结果完成后再替换列表，键盘钩不被同步 I/O 阻塞。
        UpdateSearchUI();
        List<ClipboardEntry> dbItems;
        try
        {
            dbItems = await Task.Run(
                () => _historyStore.Search(
                    query,
                    typeFilter,
                    100,
                    quickPhraseOnly,
                    searchColdArchives,
                    cancellation.Token),
                cancellation.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        finally
        {
            if (ReferenceEquals(_searchRefreshCancellation, cancellation))
                _searchRefreshCancellation = null;
            cancellation.Dispose();
        }

        if (generation != _searchRefreshGeneration
            || !string.Equals(query, _searchText, StringComparison.Ordinal)
            || typeFilter != _typeFilter
            || quickPhraseOnly != _quickPhraseOnly
            || searchColdArchives != _searchColdArchives)
        {
            return;
        }

        ApplyFilterResults(query, spec, dbItems, null);
    }

    private void CancelPendingSearchRefresh()
    {
        var pending = _searchRefreshCancellation;
        _searchRefreshCancellation = null;
        if (pending == null) return;
        pending.Cancel();
    }

    private void ApplyFilterResults(
        string query,
        SearchQuerySpec spec,
        List<ClipboardEntry> dbItems,
        int? preferSelectListIndex)
    {
        CloseEntryPreviewBubble();
        ClearPendingDelete();
        _firstVisibleIndex = 0;

        IEnumerable<ClipboardEntry> quickPastes = _allItems.Where(i => i.IsQuickPaste);
        if (_typeFilter.HasValue)
            quickPastes = quickPastes.Where(i => i.Type == _typeFilter.Value || (_typeFilter.Value == EntryType.Image && i.IsImageFile));
        if (!spec.IsEmpty)
            quickPastes = quickPastes.Where(i => i.MatchesSearch(query));

        var filtered = new List<ClipboardEntry>(quickPastes);

        var allItemsDict = _allItems.Where(i => !i.IsQuickPaste && i.PersistedId.HasValue).ToDictionary(i => i.PersistedId!.Value);
        foreach (var dbItem in dbItems)
        {
            if (dbItem.PersistedId.HasValue && allItemsDict.TryGetValue(dbItem.PersistedId.Value, out var existing))
                filtered.Add(existing);
            else
            {
                _allItems.Add(dbItem);
                filtered.Add(dbItem);
            }
        }

        var filteredSet = filtered.ToHashSet();
        UpdateBatchOrderProperties();
        var queuePart = _batchQueue.FilterQueued(filteredSet);
        var rest = filtered
            .Where(e => !_batchQueue.ContainsQueued(e))
            .OrderByDescending(i => i.IsQuickPaste && !string.IsNullOrEmpty(query))
            .ThenByDescending(i => i.IsStarred)
            .ThenByDescending(i => i.CopiedAt);

        var nextDisplayItems = new List<ClipboardEntry>(filtered.Count);
        int idx = 1;
        foreach (var item in queuePart)
        {
            item.DisplayIndex = idx++;
            nextDisplayItems.Add(item);
        }
        foreach (var item in rest)
        {
            item.DisplayIndex = idx++;
            nextDisplayItems.Add(item);
        }

        ReplaceDisplayItems(nextDisplayItems);

        UpdateSearchMetadataPreviews(spec);
        UpdateEmptyState();
        if (_displayItems.Count > 0)
        {
            int sel = preferSelectListIndex.HasValue
                ? Math.Clamp(preferSelectListIndex.Value, 0, _displayItems.Count - 1)
                : 0;
            ItemsList.SelectedIndex = sel;
            _selectionCursor.SetMouseAnchor(_displayItems.Count, sel);
            ClearKeyboardPointSelectionCursor();
            ItemsList.ScrollIntoView(ItemsList.SelectedItem);
        }
        else
        {
            _selectionCursor.Reset();
            ClearKeyboardPointSelectionCursor();
        }

        _lastAppliedFilterQuery = query;
        _lastAppliedFilterType = _typeFilter;
        _lastAppliedQuickPhraseOnly = _quickPhraseOnly;
        _lastAppliedSearchColdArchives = _searchColdArchives;
        _hasAppliedFilterResults = true;
    }

    private void ReplaceDisplayItems(IReadOnlyList<ClipboardEntry> next)
    {
        var commonCount = next.Count(item => _displayItems.Contains(item));
        var comparisonSize = Math.Max(next.Count, _displayItems.Count);
        if (comparisonSize == 0) return;

        // 大幅变化时一次 Reset 更便宜；相邻查询通常高度重合，使用 Move/Insert/Remove 保留容器与高亮缓存。
        if (commonCount * 3 < comparisonSize)
        {
            using var bulk = _displayItems.BeginBulkUpdate();
            _displayItems.Clear();
            foreach (var item in next)
                _displayItems.Add(item);
            return;
        }

        for (var targetIndex = 0; targetIndex < next.Count; targetIndex++)
        {
            var item = next[targetIndex];
            if (targetIndex < _displayItems.Count && ReferenceEquals(_displayItems[targetIndex], item))
                continue;

            var existingIndex = _displayItems.IndexOf(item);
            if (existingIndex >= 0)
                _displayItems.Move(existingIndex, targetIndex);
            else
                _displayItems.Insert(targetIndex, item);
        }

        while (_displayItems.Count > next.Count)
            _displayItems.RemoveAt(_displayItems.Count - 1);
    }

    private void UpdateEmptyState()
    {
        UpdateSearchUI();
        var hasItems = _displayItems.Count > 0;
        EmptyHint.Visibility = hasItems ? Visibility.Collapsed : Visibility.Visible;
        ItemsList.Visibility = hasItems ? Visibility.Visible : Visibility.Collapsed;

        if (_searchText.Length > 0 && !hasItems)
        {
            EmptyIcon.Source = CommandIconSvg.Get(CommandIconKind.Search); EmptyText.Text = "无匹配结果";
            EmptySubText.Text = PinyinFilterModes.Normalize(_appSettings?.PinyinFilterMode) == PinyinFilterModes.Xiaohe
                ? "可试小鹤双拼全码或声母串，如「nihc」「nh」"
                : "可试拼音全拼或首字母，如「nihao」「nh」";
        }
        else if (!hasItems)
        {
            EmptyIcon.Source = CommandIconSvg.Get(CommandIconKind.Empty); EmptyText.Text = "暂无剪切板记录"; EmptySubText.Text = "复制一些文本即可开始";
        }

        var regularCount = _allItems.Count(x => !x.IsQuickPaste);
        ItemCountText.Text = regularCount > 0 ? $"({regularCount})" : "";
    }
}
