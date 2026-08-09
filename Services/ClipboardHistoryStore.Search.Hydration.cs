using ClipboardManager.Models;
using ClipboardManager.Services;
using Microsoft.Data.Sqlite;

namespace ClipboardManager;

internal sealed partial class ClipboardHistoryStore
{
    private static void HydrateImages(SqliteConnection conn, List<ClipboardEntry> entries)
    {
        var images = entries
            .Where(entry => entry.Type == EntryType.Image && entry.PersistedId.HasValue)
            .ToArray();
        if (images.Length == 0) return;

        using var cmd = conn.CreateCommand();
        var parameters = new string[images.Length];
        for (var i = 0; i < images.Length; i++)
        {
            parameters[i] = $"@imageId{i}";
            cmd.Parameters.AddWithValue(parameters[i], images[i].PersistedId!.Value);
        }

        cmd.CommandText =
            $"SELECT id, image_blob FROM clipboard_history WHERE id IN ({string.Join(",", parameters)})";
        var byId = images.ToDictionary(entry => entry.PersistedId!.Value);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            if (byId.TryGetValue(reader.GetInt64(0), out var entry) && !reader.IsDBNull(1))
                entry.ImageData = (byte[])reader.GetValue(1);
        }
    }

    private static void HydrateArchivedImages(SqliteConnection conn, List<ClipboardEntry> entries)
    {
        foreach (var group in entries
                     .Where(entry => entry.Type == EntryType.Image && entry.IsArchived)
                     .GroupBy(entry => entry.ArchiveBucketNo!.Value))
        {
            var archived = group.ToArray();
            using var cmd = conn.CreateCommand();
            var parameters = new string[archived.Length];
            for (var i = 0; i < archived.Length; i++)
            {
                parameters[i] = $"@archiveImageId{i}";
                cmd.Parameters.AddWithValue(parameters[i], archived[i].ArchiveId!.Value);
            }

            cmd.CommandText =
                $"SELECT archive_id, image_blob FROM {ArchiveTableName(group.Key)} " +
                $"WHERE archive_id IN ({string.Join(",", parameters)})";
            var byId = archived.ToDictionary(entry => entry.ArchiveId!.Value);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                if (byId.TryGetValue(reader.GetInt64(0), out var entry) && !reader.IsDBNull(1))
                    entry.ImageData = (byte[])reader.GetValue(1);
            }
        }
    }

    private static void HydrateSourceIcons(SqliteConnection conn, List<ClipboardEntry> entries)
    {
        var iconsByPath = new Dictionary<string, byte[]?>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in entries)
        {
            var path = entry.Source?.ExePath;
            if (string.IsNullOrWhiteSpace(path)) continue;
            if (!iconsByPath.TryGetValue(path, out var icon))
            {
                // 历史查询可能发生在面板呼出路径；只读现有缓存，绝不探测来源 exe。
                icon = SourceAppIconCache.TryGetCachedIconPng(path, conn);
                iconsByPath[path] = icon;
            }

            entry.SourceIconPng = icon;
        }
    }
}
