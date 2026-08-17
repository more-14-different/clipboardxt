using ClipboardManager.Models;

namespace ClipboardManager;

internal sealed partial class ClipboardHistoryStore
{
    public List<ClipboardEntry> LoadNewestFirst(int limit) => Search("", null, limit);

    public List<ClipboardEntry> LoadNewestFirstLite(int limit) =>
        Search("", null, limit, hydrateHotImages: false);

    /// <summary>
    /// 利用 FTS5 和结构化过滤执行检索。图片 BLOB 按需从数据库懒加载。
    /// </summary>
    public List<ClipboardEntry> Search(
        string query,
        EntryType? typeFilter,
        int limit,
        bool shortcutPhraseOnly = false,
        bool searchColdArchives = true,
        CancellationToken cancellationToken = default,
        bool hydrateHotImages = true)
    {
        if (limit <= 0) return [];
        cancellationToken.ThrowIfCancellationRequested();

        var results = new List<ClipboardEntry>(Math.Min(limit, 64));
        var spec = SearchQuerySpec.Parse(query);
        try
        {
            using var conn = Open();
            var pageSize = spec.IsEmpty ? limit : Math.Clamp(limit * 4, 200, 1000);
            var offset = 0;

            while (results.Count < limit)
            {
                cancellationToken.ThrowIfCancellationRequested();
                using var cmd = CreateCandidateCommand(
                    conn,
                    spec,
                    typeFilter,
                    shortcutPhraseOnly,
                    pageSize,
                    offset);
                using var cancellationRegistration = cancellationToken.Register(() => cmd.Cancel());

                var rowsRead = 0;
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        rowsRead++;
                        var entry = ReadEntry(reader);
                        entry.SetPersistedPinyinSearchBlob(
                            reader.IsDBNull(19) ? null : reader.GetString(19),
                            PinyinFilterMode);

                        if (!spec.IsEmpty && !MatchesEntrySearchSpec(entry, spec))
                            continue;

                        results.Add(entry);
                        if (results.Count >= limit) break;
                    }
                }

                if (rowsRead < pageSize || spec.IsEmpty) break;
                offset += rowsRead;
            }

            if (searchColdArchives && CanSearchArchived(spec) && !shortcutPhraseOnly && results.Count < limit)
            {
                results.AddRange(SearchArchived(
                    conn,
                    spec,
                    typeFilter,
                    limit - results.Count,
                    cancellationToken));
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (hydrateHotImages)
                HydrateImages(conn, results);
            else
                foreach (var entry in results.Where(e => e.Type == EntryType.Image && e.PersistedId.HasValue))
                    entry.ImageDataLoader = LoadImageData;
            HydrateArchivedImages(conn, results);
            HydrateSourceIcons(conn, results);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            // 数据库不可用时保持原有降级行为：返回当前已取得的结果。
        }

        return results;
    }

    private static bool MatchesEntrySearchSpec(ClipboardEntry entry, SearchQuerySpec spec) =>
        entry.MatchesSearch(spec, PinyinFilterMode);
}
