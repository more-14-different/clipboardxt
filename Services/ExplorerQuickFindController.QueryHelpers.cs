using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace ClipboardManager;

public sealed partial class ExplorerQuickFindController : IDisposable
{    private static string TryMakeRelative(string fullPath, string basePath)
    {
        if (string.IsNullOrEmpty(basePath)) return fullPath;
        var b = basePath.TrimEnd('\\', '/');
        if (fullPath.StartsWith(b, StringComparison.OrdinalIgnoreCase) && fullPath.Length > b.Length)
            return fullPath[(b.Length + 1)..];
        return fullPath;
    }

    /// <summary>
    /// 规范化当前文件夹路径，供 Everything 的 <c>parent:</c> / <c>path:</c> 使用。
    ///
    /// 历史坑 1：在 <see cref="Path.GetFullPath"/> **之前**把 <c>C:\</c> 收成 <c>C:</c>，
    /// Windows 上 <c>GetFullPath("C:")</c> 会变成「该盘当前工作目录」而不是根目录，
    /// <c>parent:C:</c> 与 Everything 里正确的 <c>parent:C:\</c> 不一致 → 根下搜索 0 条。
    ///
    /// 历史坑 2：引号内路径末尾反斜杠 <c>\"</c> 会被 Everything 当成转义引号；非根路径
    /// 仍去掉末尾分隔符；盘符根则固定为 <c>X:\</c>（无引号、无歧义）。
    /// </summary>
    private static string NormalizeFolderForEverything(string folder)
    {
        var t = folder.Trim();
        if (t.Length == 0) return "";
        try
        {
            var full = Path.GetFullPath(t);
            if (full.Length >= 3 && full[1] == ':' && (full[2] == '\\' || full[2] == '/'))
            {
                var rest = full.Length > 3 ? full.Substring(3).TrimStart('\\', '/') : "";
                if (rest.Length == 0)
                    return $"{char.ToUpperInvariant(full[0])}:\\";
            }

            return full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
        catch
        {
            return t;
        }
    }

    /// <summary>当前文件夹<strong>一层</strong>子项 + 可选关键词。<c>parent:</c></summary>
    private static string BuildEverythingParentScopedSearch(string folder, SearchQuerySpec spec)
    {
        var f = NormalizeFolderForEverything(folder);
        var query = BuildEverythingBroadSearch(spec);
        if (f.Length == 0) return query;
        var token = "parent:" + QuotePathForEverythingToken(f);
        if (string.IsNullOrEmpty(query)) return token;
        return token + " " + query;
    }

    /// <summary>当前路径<strong>树下</strong>任意深度 + 可选关键词。<c>path:</c> 匹配完整路径前缀。</summary>
    private static string BuildEverythingPathSubtreeScopedSearch(string folder, SearchQuerySpec spec)
    {
        var f = NormalizeFolderForEverything(folder);
        var query = BuildEverythingBroadSearch(spec);
        if (f.Length == 0) return query;
        var token = "path:" + QuotePathForEverythingToken(f);
        if (string.IsNullOrEmpty(query)) return token;
        return token + " " + query;
    }

    private static string BuildEverythingBroadSearch(SearchQuerySpec spec) =>
        string.Join(" ", spec.BroadTokens.Select(QuoteEverythingTerm));

    private static string QuoteEverythingTerm(string term) =>
        "\"" + term.Replace("\"", "\\\"") + "\"";

    private static void FilterQuickFindPaths(List<string> paths, string baseFolder, SearchQuerySpec spec)
    {
        if (spec.IsEmpty) return;
        for (var i = paths.Count - 1; i >= 0; i--)
        {
            if (!spec.MatchesText(QuickFindSearchablePath(paths[i], baseFolder)))
                paths.RemoveAt(i);
        }
    }

    private static string QuickFindSearchablePath(string path, string baseFolder)
    {
        var trimmed = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var baseTrimmed = baseFolder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (!string.IsNullOrEmpty(baseTrimmed)
            && (trimmed.StartsWith(baseTrimmed + "\\", StringComparison.OrdinalIgnoreCase)
                || trimmed.StartsWith(baseTrimmed + "/", StringComparison.OrdinalIgnoreCase)))
        {
            return trimmed[(baseTrimmed.Length + 1)..];
        }

        var name = Path.GetFileName(trimmed);
        return string.IsNullOrEmpty(name) ? trimmed : name;
    }

    /// <summary>路径含空格时用双引号包住；末尾不应有反斜杠（避免 \" 被解释为转义引号）。</summary>
    private static string QuotePathForEverythingToken(string path)
    {
        if (path.IndexOf(' ', StringComparison.Ordinal) >= 0)
            return "\"" + path + "\"";
        return path;
    }

    /// <summary>先保留 <paramref name="first"/> 顺序，再追加 <paramref name="second"/> 中未出现的路径，最多 <paramref name="maxCount"/> 条。</summary>
    private static List<string> MergePathListsPreferFirst(
        IReadOnlyList<string> first,
        IReadOnlyList<string> second,
        int maxCount)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var merged = new List<string>(Math.Min(maxCount, first.Count + second.Count));
        foreach (var p in first)
        {
            if (seen.Add(p)) merged.Add(p);
            if (merged.Count >= maxCount) return merged;
        }

        foreach (var p in second)
        {
            if (seen.Add(p)) merged.Add(p);
            if (merged.Count >= maxCount) return merged;
        }

        return merged;
    }

    // ===================== 就地导航 + 选中 =====================

    /// <summary>
    /// 在资源管理器中就地导航到目标文件所在文件夹并选中该文件。
    /// Shell COM late-binding → fallback SHOpenFolderAndSelectItems。
}

