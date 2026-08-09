using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace ClipboardManager;

public partial class FileDialogJumpPickerWindow : Window
{
    private void BuildMasterList()
    {
        var sw = Stopwatch.StartNew();
        var favCount = _settings.FolderFavorites.Count;
        var snapshotCount = _collectorSnapshot.Count;
        _masterRows.Clear();
        var allPaths = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var favoriteByPath = new Dictionary<string, (FolderFavoriteEntry Entry, int Rank)>(StringComparer.OrdinalIgnoreCase);
        var recentRankByPath = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var contextByPath = new Dictionary<string, (string Label, int Rank)>(StringComparer.OrdinalIgnoreCase);

        static string? NormalizeDirectoryPath(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            try
            {
                return Path.GetFullPath(raw.Trim());
            }
            catch
            {
                return null;
            }
        }

        void AddPath(string path)
        {
            if (seen.Add(path)) allPaths.Add(path);
        }

        for (var i = 0; i < _settings.RecentFileDialogFolders.Count; i++)
        {
            var full = NormalizeDirectoryPath(_settings.RecentFileDialogFolders[i]);
            if (full == null || recentRankByPath.ContainsKey(full)) continue;
            recentRankByPath.Add(full, i);
            AddPath(full);
        }

        for (var i = 0; i < _settings.FolderFavorites.Count; i++)
        {
            var fav = _settings.FolderFavorites[i];
            var full = NormalizeDirectoryPath(fav.Path);
            if (full == null || favoriteByPath.ContainsKey(full)) continue;
            favoriteByPath.Add(full, (fav, i));
            AddPath(full);
        }

        for (var i = 0; i < _collectorSnapshot.Count; i++)
        {
            var candidate = _collectorSnapshot[i];
            var full = NormalizeDirectoryPath(candidate.Path);
            if (full == null || contextByPath.ContainsKey(full)) continue;
            contextByPath.Add(full, (candidate.Label, i));
            AddPath(full);
        }

        foreach (var path in allPaths)
        {
            var isFavorite = favoriteByPath.TryGetValue(path, out var favorite);
            var isRecent = recentRankByPath.TryGetValue(path, out var recentRank);
            var hasContext = contextByPath.TryGetValue(path, out var context);
            var sourceLabel = hasContext ? context.Label : isRecent ? "常用" : "收藏";
            _masterRows.Add(new FileJumpPickerRow(
                sourceLabel,
                path,
                isFavorite,
                isFavorite ? favorite.Entry.Phrase : null,
                isRecent,
                isRecent ? recentRank : int.MaxValue,
                isFavorite ? favorite.Rank : int.MaxValue,
                hasContext ? context.Rank : int.MaxValue));
        }

        sw.Stop();
        PerfLog("build_master_list", sw.ElapsedMilliseconds, 25,
            $"fav={favCount} snapshot={snapshotCount} rows={_masterRows.Count}");
    }

    private void RefreshFilter(int? preferListIndex = null, string? preferPath = null, bool scrollSelection = true)
    {
        var sw = Stopwatch.StartNew();
        CloseJumpPreviewBubble();
        var keepPath = preferPath ?? (ItemsList.SelectedItem as FileJumpPickerRow)?.Path;
        _firstVisibleIndex = 0;

        var query = _searchText;
        var spec = SearchQuerySpec.Parse(query);
        if (spec.IsEmpty || !_settings.FileJumpPickerEverythingFolderSearch)
        {
            _everythingFolderPaths.Clear();
            _everythingPathsValidForQuery = "";
            _everythingQueryCts?.Cancel();
        }
        else if (!string.Equals(query, _everythingPathsValidForQuery, StringComparison.OrdinalIgnoreCase))
        {
            _everythingFolderPaths.Clear();
            _everythingPathsValidForQuery = "";
        }

        using (var _ = _displayRows.BeginBulkUpdate())
        {
            _displayRows.Clear();

            IEnumerable<FileJumpPickerRow> seq = _masterRows;
            seq = _filterMode switch
            {
                FileJumpPickerFilterMode.FavoritesOnly => seq.Where(r => r.IsFavorite),
                FileJumpPickerFilterMode.RecentOnly => seq.Where(r => r.IsRecentFolder),
                _ => seq
            };

            if (!spec.IsEmpty)
                seq = seq.Where(r => r.MatchesSearch(query));

            var sorted = FileJumpPickerRowOrdering.OrderForDisplay(seq);

            foreach (var r in sorted)
            {
                r.DisplayMetadataChips = r.BuildMetadataChips(spec);
                _displayRows.Add(r);
            }

            if (_filterMode != FileJumpPickerFilterMode.FavoritesOnly
                && !spec.IsEmpty
                && _settings.FileJumpPickerEverythingFolderSearch
                && string.Equals(query, _everythingPathsValidForQuery, StringComparison.OrdinalIgnoreCase)
                && _everythingFolderPaths.Count > 0)
            {
                var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var r in _displayRows)
                    seenPaths.Add(r.Path);

                foreach (var p in _everythingFolderPaths.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
                {
                    if (!seenPaths.Add(p)) continue;
                    var row = new FileJumpPickerRow("everything", p, false);
                    row.DisplayMetadataChips = row.BuildMetadataChips(spec);
                    _displayRows.Add(row);
                }
            }
        }

        _prevQuickIndexFirstVisible = -1;
        AssignVisibleQuickIndices(0);

        UpdateSearchChrome();
        if (_displayRows.Count > 0)
        {
            int sel = 0;
            if (!string.IsNullOrEmpty(keepPath))
            {
                var i = _displayRows.ToList().FindIndex(r =>
                    string.Equals(r.Path, keepPath, StringComparison.OrdinalIgnoreCase));
                if (i >= 0) sel = i;
                else if (preferListIndex.HasValue)
                    sel = Math.Clamp(preferListIndex.Value, 0, _displayRows.Count - 1);
            }
            else if (preferListIndex.HasValue)
                sel = Math.Clamp(preferListIndex.Value, 0, _displayRows.Count - 1);

            ItemsList.SelectedIndex = sel;
            if (scrollSelection)
                ItemsList.ScrollIntoView(ItemsList.SelectedItem);
        }

        if (!spec.IsEmpty && _settings.FileJumpPickerEverythingFolderSearch)
            ScheduleEverythingFolderQuery(query);

        sw.Stop();
        PerfLog("refresh_filter", sw.ElapsedMilliseconds, 25,
            $"queryLen={query.Length} master={_masterRows.Count} display={_displayRows.Count}");
    }

    private void ScheduleEverythingFolderQuery(string queryForSchedule)
    {
        if (!_settings.FileJumpPickerEverythingFolderSearch || string.IsNullOrEmpty(queryForSchedule))
        {
            _everythingQueryCts?.Cancel();
            return;
        }

        if (string.Equals(queryForSchedule, _everythingPathsValidForQuery, StringComparison.OrdinalIgnoreCase))
            return;

        _everythingQueryGen++;
        var gen = _everythingQueryGen;
        _everythingQueryCts?.Cancel();
        _everythingQueryCts = new CancellationTokenSource();
        var tok = _everythingQueryCts.Token;
        var maxResults = Math.Clamp(_settings.ExplorerEverythingQuickFindMaxResults, 1, 2000);

        // 早期 debounce 写到 140ms，对「换一段输入再按 Tab/字母」的交互而言体感很重。
        // Everything IPC 文件夹检索单次开销通常 <10ms，节流 40ms 足以合并连按又不感知卡顿。
        _ = Task.Run(() =>
        {
            try
            {
                if (tok.WaitHandle.WaitOne(40)) return;
                if (gen != _everythingQueryGen) return;

                var list = new List<string>();
                var ok = EverythingIpc.TryQueryFolderPaths(queryForSchedule, maxResults, list, out _);

                Dispatcher.BeginInvoke(() =>
                {
                    if (gen != _everythingQueryGen) return;
                    if (!string.Equals(_searchText, queryForSchedule, StringComparison.Ordinal)) return;

                    _everythingFolderPaths.Clear();
                    if (ok)
                        _everythingFolderPaths.AddRange(list);
                    _everythingPathsValidForQuery = queryForSchedule;

                    var pathKeep = (ItemsList.SelectedItem as FileJumpPickerRow)?.Path;
                    RefreshFilter(preferPath: pathKeep);
                }, DispatcherPriority.Background);
            }
            catch
            {
                /* ignore */
            }
        });
    }

    private int _prevQuickIndexFirstVisible = -1;

    private void AssignVisibleQuickIndices(int firstVisible)
    {
        var count = _displayRows.Count;
        if (count == 0) { _prevQuickIndexFirstVisible = firstVisible; return; }

        // 只更新 old/new 可见窗口的并集（最多 18 行），而非全部遍历。
        var oldFirst = _prevQuickIndexFirstVisible;
        _prevQuickIndexFirstVisible = firstVisible;

        int newLast = Math.Min(firstVisible + 8, count - 1);
        int oldLast = oldFirst >= 0 ? Math.Min(oldFirst + 8, count - 1) : -1;

        int rangeStart = Math.Max(0, Math.Min(firstVisible, oldFirst >= 0 ? oldFirst : firstVisible));
        int rangeEnd = Math.Max(newLast, oldLast);

        for (int i = rangeStart; i <= rangeEnd; i++)
        {
            int rel = i - firstVisible + 1;
            _displayRows[i].DisplayIndex = rel is >= 1 and <= 9 ? rel : 0;
        }
    }
}
