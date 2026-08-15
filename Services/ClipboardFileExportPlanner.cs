using System.Globalization;
using System.Text.Json;

namespace ClipboardManager;

internal static class ClipboardFileExportPlanner
{
    internal enum ItemKind
    {
        ExistingPath,
        Text,
        Json,
        Png,
    }

    internal readonly record struct Item(
        ItemKind Kind,
        string? ExistingPath = null,
        string? Text = null,
        byte[]? ImageData = null);

    internal static IReadOnlyList<Item> Build(IEnumerable<ClipboardEntry> entries)
    {
        var items = new List<Item>();
        foreach (var entry in entries)
        {
            switch (entry.Type)
            {
                case EntryType.Text when !string.IsNullOrEmpty(entry.TextContent):
                    items.Add(new Item(
                        IsWellFormedJson(entry.TextContent) ? ItemKind.Json : ItemKind.Text,
                        Text: entry.TextContent));
                    break;

                case EntryType.Image when entry.TryGetImageData() is { Length: > 0 } imageData:
                    items.Add(new Item(ItemKind.Png, ImageData: imageData));
                    break;

                case EntryType.Files when entry.FilePaths is { Length: > 0 } paths:
                    foreach (var path in paths)
                    {
                        if (!string.IsNullOrWhiteSpace(path))
                            items.Add(new Item(ItemKind.ExistingPath, ExistingPath: path.Trim()));
                    }
                    break;
            }
        }

        return items;
    }

    internal static bool CanExport(ClipboardEntry entry) => entry.Type switch
    {
        EntryType.Text => !string.IsNullOrEmpty(entry.TextContent),
        EntryType.Image => true,
        EntryType.Files => entry.FilePaths?.Any(path => !string.IsNullOrWhiteSpace(path)) == true,
        _ => false,
    };

    internal static bool IsWellFormedJson(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        try
        {
            using var _ = JsonDocument.Parse(
                text,
                new JsonDocumentOptions
                {
                    AllowTrailingCommas = false,
                    CommentHandling = JsonCommentHandling.Disallow,
                });
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    internal static string BuildTempFileName(
        DateTime timestamp,
        string batchToken,
        int oneBasedIndex,
        ItemKind kind)
    {
        var extension = kind switch
        {
            ItemKind.Text => ".txt",
            ItemKind.Json => ".json",
            ItemKind.Png => ".png",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };
        return string.Create(
            CultureInfo.InvariantCulture,
            $"clip_{timestamp:yyyyMMdd_HHmmss_fff}_{oneBasedIndex:D3}_{batchToken}{extension}");
    }
}
