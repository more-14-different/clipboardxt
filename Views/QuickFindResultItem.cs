using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Brush = System.Windows.Media.Brush;

namespace ClipboardManager;

public sealed class QuickFindResultItem
{
    public string FullPath { get; init; } = "";
    public string FileName { get; init; } = "";
    public string RelativePath { get; init; } = "";
    public bool IsDirectory { get; init; }

    /// <summary>来自第二次「全盘」关键词检索的补充项（与 <see cref="FromScopedAndGlobalLists"/> 配套）。</summary>
    public bool IsGlobalMatch { get; init; }

    /// <summary>
    /// 先「当前路径」父目录限定，再「全盘」关键词；去重后前者在前，全盘补充项标 <see cref="IsGlobalMatch"/>。
    /// </summary>
    public static List<QuickFindResultItem> FromScopedAndGlobalLists(
        IReadOnlyList<string> scopedPaths,
        IReadOnlyList<string>? globalPaths,
        string baseFolder,
        int maxTotal)
    {
        var scopedSet = new HashSet<string>(scopedPaths, StringComparer.OrdinalIgnoreCase);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var merged = new List<string>();
        foreach (var p in scopedPaths)
        {
            if (seen.Add(p)) merged.Add(p);
            if (merged.Count >= maxTotal) goto build;
        }

        if (globalPaths != null)
        {
            foreach (var p in globalPaths)
            {
                if (seen.Add(p)) merged.Add(p);
                if (merged.Count >= maxTotal) break;
            }
        }

    build:
        var split = 0;
        for (; split < merged.Count; split++)
        {
            if (!scopedSet.Contains(merged[split])) break;
        }

        var onlyScoped = merged.Take(split).ToList();
        var onlyGlobal = merged.Skip(split).ToList();

        var scopedItems = FromFullPaths(onlyScoped, baseFolder);
        var baseTrimmed = baseFolder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var globalItems = new List<QuickFindResultItem>();
        foreach (var p in onlyGlobal.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
        {
            var trimmed = p.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var name = Path.GetFileName(trimmed);
            if (string.IsNullOrEmpty(name)) name = trimmed;

            string rel;
            if (trimmed.StartsWith(baseTrimmed + "\\", StringComparison.OrdinalIgnoreCase)
                || trimmed.StartsWith(baseTrimmed + "/", StringComparison.OrdinalIgnoreCase))
                rel = trimmed[(baseTrimmed.Length + 1)..];
            else
                rel = name;

            globalItems.Add(new QuickFindResultItem
            {
                FullPath = p,
                FileName = name,
                RelativePath = rel,
                IsDirectory = Directory.Exists(p),
                IsGlobalMatch = true,
            });
        }

        return scopedItems.Concat(globalItems).ToList();
    }

    public static List<QuickFindResultItem> FromFullPaths(IReadOnlyList<string> paths, string baseFolder)
    {
        var baseTrimmed = baseFolder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var list = new List<QuickFindResultItem>(paths.Count);
        foreach (var p in paths)
        {
            var trimmed = p.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var name = Path.GetFileName(trimmed);
            if (string.IsNullOrEmpty(name)) name = trimmed;

            string rel;
            if (trimmed.StartsWith(baseTrimmed + "\\", StringComparison.OrdinalIgnoreCase)
                || trimmed.StartsWith(baseTrimmed + "/", StringComparison.OrdinalIgnoreCase))
                rel = trimmed[(baseTrimmed.Length + 1)..];
            else
                rel = name;

            var isDir = Directory.Exists(p);

            list.Add(new QuickFindResultItem
            {
                FullPath = p,
                FileName = name,
                RelativePath = rel,
                IsDirectory = isDir,
            });
        }

        // 当前目录直属文件排最前，子目录文件按路径深度排后
        list.Sort((a, b) =>
        {
            int depthA = a.RelativePath.Count(c => c is '\\' or '/');
            int depthB = b.RelativePath.Count(c => c is '\\' or '/');
            if (depthA != depthB) return depthA.CompareTo(depthB);
            return string.Compare(a.RelativePath, b.RelativePath, StringComparison.OrdinalIgnoreCase);
        });

        return list;
    }
}
