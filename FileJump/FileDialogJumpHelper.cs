using System.IO;

namespace ClipboardManager;

/// <summary>执行文件对话框路径跳转。</summary>
internal static partial class FileDialogJumpHelper
{
    /// <param name="allowShellInject">为 false 时不注入宿主进程，仅走 UIA/地址栏等模拟。</param>
    public static bool TryNavigateToFolder(
        IntPtr dialogHwnd,
        string folderPath,
        bool allowShellInject = true)
    {
        var path = NormalizeFolderPathForNavigation(folderPath);
        if (!Directory.Exists(path)) return false;

        if (TryReadCurrentFolder(dialogHwnd, out var currentFolder, relaxed: true)
            && PathsLooselyEqual(currentFolder, path))
        {
            ShellNavigateLog.Write(
                "filejump",
                $"skip navigate already at target path=\"{path}\" hwnd=0x{dialogHwnd.ToInt64():X}");
            return true;
        }

        var kind = ClassifyFileDialog(dialogHwnd);
        var customRule = kind == FileDialogKind.None
            ? CustomFileDialogStore.FindMatchingRule(dialogHwnd)
            : null;

        if (kind == FileDialogKind.None && customRule != null)
            return TryNavigateCustomRule(dialogHwnd, path, allowShellInject, customRule);

        if (kind == FileDialogKind.None) return false;

        if (allowShellInject
            && Win32.GetWindowClassName(dialogHwnd).Equals("#32770", StringComparison.Ordinal))
        {
            if (ShellDialogDeepNavigate.TryBrowseObjectInject(dialogHwnd, path))
                return true;

            Thread.Sleep(80);
            if (TryReadCurrentFolder(dialogHwnd, out var afterInject, relaxed: true)
                && PathsLooselyEqual(afterInject, path))
            {
                ShellNavigateLog.Write(
                    "filejump",
                    $"shell inject reached target despite failure status path=\"{path}\" hwnd=0x{dialogHwnd.ToInt64():X}");
                return true;
            }
        }

        if (kind == FileDialogKind.SysListView)
            return TryNavigateSysListViewStyle(dialogHwnd, path);
        if (kind == FileDialogKind.WpsCustom)
            return TryNavigateWpsCustom(dialogHwnd, path);
        return TryNavigateAddressBarStyle(dialogHwnd, path);
    }

    private static bool PathsLooselyEqual(string left, string right)
    {
        if (string.IsNullOrEmpty(left) || string.IsNullOrEmpty(right)) return false;
        try
        {
            var normalizedLeft = Path.GetFullPath(left).TrimEnd('\\', '/');
            var normalizedRight = Path.GetFullPath(right).TrimEnd('\\', '/');
            return string.Equals(normalizedLeft, normalizedRight, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }
}
