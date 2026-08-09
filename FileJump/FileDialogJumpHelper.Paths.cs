using System.IO;

namespace ClipboardManager;

internal static partial class FileDialogJumpHelper
{
    /// <summary>供路径采集等模块复用：从 UI 文本还原为已存在的目录路径。</summary>
    internal static bool TryNormalizeToExistingDirectory(string? raw, out string norm)
    {
        norm = "";
        if (string.IsNullOrWhiteSpace(raw)) return false;
        var value = raw.Trim();
        if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
            value = value[1..^1].Trim();

        try
        {
            if (value.Length == 2 && value[1] == ':' && char.IsLetter(value[0]))
            {
                var root = char.ToUpperInvariant(value[0]) + @":\";
                if (Directory.Exists(root))
                {
                    norm = Path.GetFullPath(root);
                    return true;
                }
            }

            if (!Path.IsPathFullyQualified(value))
                return false;

            if (Directory.Exists(value))
            {
                norm = Path.GetFullPath(value);
                return true;
            }

            var directory = Path.GetDirectoryName(value);
            if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
            {
                norm = Path.GetFullPath(directory);
                return true;
            }
        }
        catch { }
        return false;
    }

    /// <summary>将联接点或符号链接解析为物理路径，失败时沿用原路径。</summary>
    private static string NormalizeFolderPathForNavigation(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath)) return folderPath;
        try
        {
            var full = Path.GetFullPath(folderPath.Trim());
            if (!Directory.Exists(full)) return full;
            var directory = new DirectoryInfo(full);
            DirectoryInfo? concrete = null;
            try
            {
                if (directory.LinkTarget != null
                    || (directory.Attributes & FileAttributes.ReparsePoint) != 0)
                {
                    var resolved = directory.ResolveLinkTarget(returnFinalTarget: true);
                    if (resolved is DirectoryInfo target && target.Exists)
                        concrete = target;
                }
            }
            catch { /* 非链接或无权解析则沿用 full */ }

            if (concrete != null && concrete.Exists)
                return concrete.FullName.TrimEnd('\\', '/');
            return full.TrimEnd('\\', '/');
        }
        catch
        {
            return folderPath;
        }
    }
}
