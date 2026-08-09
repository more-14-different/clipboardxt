using System;
using System.Windows;

namespace ClipboardManager;

public partial class PopupWindow : Window
{
    private static string NormalizeFolderPathForCompare(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return "";
        return path.Trim().TrimEnd('\\', '/');
    }

    private void MarkFileJumpNavigationSuppressed(IntPtr dialogHwnd, string path)
    {
        var dialogRoot = dialogHwnd != IntPtr.Zero && Win32.IsWindow(dialogHwnd)
            ? Win32.GetAncestor(dialogHwnd, Win32.GA_ROOT)
            : IntPtr.Zero;
        _fileJumpNavigationSuppressRoot = dialogRoot;
        _fileJumpNavigationSuppressPath = NormalizeFolderPathForCompare(path);
        _fileJumpNavigationSuppressUntilTick = Environment.TickCount64 + 1500;
    }

    private bool IsFileJumpNavigationSuppressed(IntPtr dialogHwnd, string? path = null)
    {
        if (_fileJumpNavigationSuppressRoot == IntPtr.Zero)
            return false;
        if (_fileJumpNavigationSuppressUntilTick != 0
            && Environment.TickCount64 > _fileJumpNavigationSuppressUntilTick)
        {
            _fileJumpNavigationSuppressRoot = IntPtr.Zero;
            _fileJumpNavigationSuppressPath = "";
            _fileJumpNavigationSuppressUntilTick = 0;
            return false;
        }
        if (dialogHwnd == IntPtr.Zero || !Win32.IsWindow(dialogHwnd))
            return false;

        var dialogRoot = Win32.GetAncestor(dialogHwnd, Win32.GA_ROOT);
        if (dialogRoot == IntPtr.Zero || dialogRoot != _fileJumpNavigationSuppressRoot)
            return false;

        var normalizedPath = NormalizeFolderPathForCompare(path);
        return string.IsNullOrEmpty(normalizedPath)
            || string.IsNullOrEmpty(_fileJumpNavigationSuppressPath)
            || string.Equals(normalizedPath, _fileJumpNavigationSuppressPath, StringComparison.OrdinalIgnoreCase);
    }
}
