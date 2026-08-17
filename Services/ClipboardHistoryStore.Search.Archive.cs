using ClipboardManager.Models;
using Microsoft.Data.Sqlite;

namespace ClipboardManager;

internal sealed partial class ClipboardHistoryStore
{
    private static bool CanSearchArchived(SearchQuerySpec spec) =>
        !spec.IsEmpty
        && spec.BroadTokens.Length > 0
        && spec.BroadTokens.All(token =>
            token.Length >= 3 || WebUrlLauncher.IsMetadataSearchToken(token, PinyinFilterMode));

    private static List<ClipboardEntry> SearchArchived(
        SqliteConnection conn,
        SearchQuerySpec spec,
        EntryType? typeFilter,
        int limit,
        CancellationToken cancellationToken)
    {
        var bucketsByNewest = new List<(int BucketNo, long MaxCopiedAtMs)>();
        using (var buckets = conn.CreateCommand())
        {
            buckets.CommandText =
                """
                SELECT bucket_no, COALESCE(max_copied_at_ms, 0)
                FROM clipboard_history_archive_buckets
                WHERE item_count > 0
                ORDER BY max_copied_at_ms DESC, bucket_no DESC
                """;
            using var cancellationRegistration = cancellationToken.Register(() => buckets.Cancel());
            using var reader = buckets.ExecuteReader();
            while (reader.Read())
            {
                cancellationToken.ThrowIfCancellationRequested();
                bucketsByNewest.Add((reader.GetInt32(0), reader.GetInt64(1)));
            }
        }

        var matches = new List<ClipboardEntry>();
        var pageSize = Math.Clamp(limit * 4, 200, 1000);
        foreach (var (bucketNo, maxCopiedAtMs) in bucketsByNewest)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (matches.Count >= limit && FromMs(maxCopiedAtMs) <= matches[^1].CopiedAt)
                break;

            var bucketMatches = new List<ClipboardEntry>(limit);
            var offset = 0;
            while (bucketMatches.Count < limit)
            {
                cancellationToken.ThrowIfCancellationRequested();
                using var cmd = CreateArchiveCandidateCommand(
                    conn,
                    bucketNo,
                    spec,
                    typeFilter,
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
                        var archiveId = reader.GetInt64(0);
                        var entry = ReadEntry(reader);
                        entry.PersistedId = null;
                        entry.ArchiveBucketNo = bucketNo;
                        entry.ArchiveId = archiveId;
                        entry.SetPersistedPinyinSearchBlob(
                            reader.IsDBNull(19) ? null : reader.GetString(19),
                            PinyinFilterMode);
                        if (MatchesEntrySearchSpec(entry, spec))
                        {
                            bucketMatches.Add(entry);
                            if (bucketMatches.Count >= limit) break;
                        }
                    }
                }

                if (rowsRead < pageSize) break;
                offset += rowsRead;
            }

            matches.AddRange(bucketMatches);
            matches = matches
                .OrderByDescending(entry => entry.CopiedAt)
                .Take(limit)
                .ToList();
        }

        return matches;
    }

    private static SqliteCommand CreateArchiveCandidateCommand(
        SqliteConnection conn,
        int bucketNo,
        SearchQuerySpec spec,
        EntryType? typeFilter,
        int pageSize,
        int offset)
    {
        var table = ArchiveTableName(bucketNo);
        var fts = $"{table}_fts";
        var whereClauses = new List<string>();
        AddArchiveSearchTokenClauses(spec, fts, whereClauses);
        if (typeFilter.HasValue)
        {
            whereClauses.Add(typeFilter.Value == EntryType.Image
                ? BuildImageTypeClause()
                : "c.entry_type = @typeFilter");
        }

        var sql =
            $"""
             SELECT c.archive_id, c.entry_type, c.text_content, NULL AS image_blob, c.image_w, c.image_h,
                    c.file_paths_json, c.copied_at_ms,
                    NULL, NULL, NULL, NULL, NULL, NULL, 0, 0, NULL, 0, NULL,
                    c.pinyin_blob
             FROM {table} c
             """;
        if (whereClauses.Count > 0)
            sql += " WHERE " + string.Join(" AND ", whereClauses);
        sql += " ORDER BY c.copied_at_ms DESC, c.archive_id DESC LIMIT @pageSize OFFSET @offset";

        var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        AddSearchTokenParameters(cmd, spec);
        if (typeFilter.HasValue && typeFilter.Value != EntryType.Image)
            cmd.Parameters.AddWithValue("@typeFilter", (int)typeFilter.Value);
        cmd.Parameters.AddWithValue("@pageSize", pageSize);
        cmd.Parameters.AddWithValue("@offset", offset);
        return cmd;
    }

    private static void AddArchiveSearchTokenClauses(
        SearchQuerySpec spec,
        string ftsTable,
        List<string> whereClauses)
    {
        if (spec.AnchorStart && !string.IsNullOrWhiteSpace(spec.StartNeedle))
            whereClauses.Add(BuildArchiveValueLikeClause("@likeQStart", includePinyin: false));
        if (spec.AnchorEnd && !string.IsNullOrWhiteSpace(spec.EndNeedle))
            whereClauses.Add(BuildArchiveValueLikeClause("@likeQEnd", includePinyin: false));

        for (var i = 0; i < spec.BroadTokens.Length; i++)
        {
            if (WebUrlLauncher.IsMetadataSearchToken(spec.BroadTokens[i], PinyinFilterMode))
            {
                whereClauses.Add(spec.BroadTokens[i].Length >= 3
                    ? $"({BuildWebUrlCandidateClause()} OR " +
                      $"c.archive_id IN (SELECT rowid FROM {ftsTable} WHERE {ftsTable} MATCH @q{i}) OR " +
                      $"{BuildArchiveValueLikeClause($"@likeQ{i}", includePinyin: true)})"
                    : $"({BuildWebUrlCandidateClause()} OR " +
                      $"{BuildArchiveValueLikeClause($"@likeQ{i}", includePinyin: true)})");
            }
            else if (spec.BroadTokens[i].Length >= 3)
            {
                whereClauses.Add(
                    $"(c.archive_id IN (SELECT rowid FROM {ftsTable} WHERE {ftsTable} MATCH @q{i}) OR " +
                    $"{BuildArchiveValueLikeClause($"@likeQ{i}", includePinyin: true)})");
            }
            else
            {
                whereClauses.Add(BuildArchiveValueLikeClause($"@likeQ{i}", includePinyin: true));
            }
        }
    }

    private static string BuildArchiveValueLikeClause(string parameter, bool includePinyin)
    {
        var clauses = new List<string>
        {
            $"c.text_content LIKE {parameter} ESCAPE '\\'",
            $"EXISTS (SELECT 1 FROM json_each(COALESCE(c.file_paths_json, '[]')) f WHERE CAST(f.value AS TEXT) LIKE {parameter} ESCAPE '\\')",
            $"printf('%dx%d', c.image_w, c.image_h) LIKE {parameter} ESCAPE '\\'",
        };
        if (includePinyin)
            clauses.Add($"c.pinyin_blob LIKE {parameter} ESCAPE '\\'");
        return $"({string.Join(" OR ", clauses)})";
    }
}
