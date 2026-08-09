using System.IO;

namespace ClipboardManager;

internal sealed class FileJumpCandidateAccumulator
{
    private readonly List<FileJumpCandidate> _items = [];
    private readonly HashSet<string> _seen = new(StringComparer.OrdinalIgnoreCase);

    internal int Count => _items.Count;
    internal int AddedCount { get; private set; }
    internal List<FileJumpCandidate> Result => _items;

    internal void Add(string label, string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        try
        {
            var normalized = Path.GetFullPath(path);
            if (!_seen.Add(normalized)) return;
            _items.Add(new FileJumpCandidate(label, normalized));
            AddedCount++;
        }
        catch
        {
            // 单个无效目录不影响其余候选。
        }
    }

    internal void AppendFavoriteFolders(
        string? memoryFolder,
        IReadOnlyList<string>? recentFolders)
    {
        if (recentFolders != null && recentFolders.Count > 0)
        {
            var index = 1;
            foreach (var rawPath in recentFolders)
            {
                if (string.IsNullOrWhiteSpace(rawPath)) continue;
                Add($"常用路径{index}", rawPath.Trim());
                index++;
            }
            return;
        }

        if (!string.IsNullOrWhiteSpace(memoryFolder))
            Add("常用路径1", memoryFolder.Trim());
    }
}
