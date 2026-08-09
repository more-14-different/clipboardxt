using System.IO;
using System.Text.RegularExpressions;

namespace ClipboardManager;

internal static partial class FileDialogJumpHelper
{
    internal static bool TryWpsBreadcrumbTextToFolder(string? text, out string folder)
    {
        folder = "";
        if (string.IsNullOrWhiteSpace(text)) return false;

        text = text.Replace('＞', '>').Replace('›', '>').Trim();
        if (TryNormalizeToExistingDirectory(text.Replace(" > ", "\\"), out folder))
            return true;

        if (!text.Contains('>'))
            return false;

        var parts = text.Split('>', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0) return false;

        string? volume = null;
        foreach (var partRaw in parts)
        {
            var part = partRaw.Trim();
            if (part.Length == 0) continue;

            if (part.Contains("此电脑", StringComparison.Ordinal)
                || part.Equals("This PC", StringComparison.OrdinalIgnoreCase)
                || part.Equals("Computer", StringComparison.OrdinalIgnoreCase))
            {
                volume = "";
                continue;
            }

            if (TryDriveRootFromBreadcrumbSegment(part, out var driveRoot))
            {
                volume = driveRoot;
                continue;
            }

            if (WpsKnownFolderToPath(part) is { } known)
            {
                volume = known;
                continue;
            }

            if (volume == null)
                continue;
            if (volume.Length == 0)
                continue;

            try
            {
                var next = Path.Combine(volume, part);
                volume = Path.GetFullPath(next);
            }
            catch
            {
                return false;
            }
        }

        if (string.IsNullOrEmpty(volume)) return false;
        try
        {
            if (!Directory.Exists(volume)) return false;
            folder = Path.GetFullPath(volume);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryDriveRootFromBreadcrumbSegment(string segment, out string driveRoot)
    {
        driveRoot = "";
        if (TryNormalizeToExistingDirectory(segment, out var direct))
        {
            try
            {
                if (Directory.Exists(direct))
                {
                    driveRoot = Path.GetFullPath(direct);
                    return true;
                }
            }
            catch
            {
                /* ignore */
            }
        }

        var i = segment.LastIndexOf('(');
        if (i >= 0)
        {
            var j = segment.IndexOf(')', i);
            if (j > i)
            {
                var inner = segment.AsSpan(i + 1, j - i - 1).Trim();
                if (inner.Length >= 2 && inner[^1] == ':' && char.IsLetter(inner[0]))
                {
                    driveRoot = char.ToUpperInvariant(inner[0]) + @":\";
                    return true;
                }
                if (inner.Length == 1 && char.IsLetter(inner[0]))
                {
                    driveRoot = char.ToUpperInvariant(inner[0]) + @":\";
                    return true;
                }
            }
        }

        var match = Regex.Match(segment, @"^([A-Za-z]):$");
        if (match.Success)
        {
            driveRoot = char.ToUpperInvariant(match.Groups[1].Value[0]) + @":\";
            return true;
        }

        return false;
    }

    private static string? WpsKnownFolderToPath(string display)
    {
        try
        {
            if (display.Contains("桌面", StringComparison.Ordinal))
                return Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            if (display.Contains("文档", StringComparison.Ordinal))
                return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            if (display.Contains("下载", StringComparison.Ordinal))
            {
                var down = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
                return Directory.Exists(down) ? down : null;
            }
            if (display.Contains("图片", StringComparison.Ordinal))
                return Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
            if (display.Contains("音乐", StringComparison.Ordinal))
                return Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);
            if (display.Contains("视频", StringComparison.Ordinal))
                return Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);

            if (display.Equals("Desktop", StringComparison.OrdinalIgnoreCase))
                return Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            if (display.Equals("Documents", StringComparison.OrdinalIgnoreCase))
                return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            if (display.Equals("Downloads", StringComparison.OrdinalIgnoreCase))
            {
                var down = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
                return Directory.Exists(down) ? down : null;
            }
        }
        catch
        {
            /* ignore */
        }

        return null;
    }
}
