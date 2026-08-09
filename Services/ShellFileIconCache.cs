using System.Collections.Concurrent;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace ClipboardManager.Services;

/// <summary>
/// 从 Windows Shell 取得文件类型或文件夹图标。缓存键按扩展名归一化，既避免列表滚动时重复调用
/// Shell，也允许源文件已移动或删除的历史项继续显示可识别的类型图标。
/// </summary>
internal static class ShellFileIconCache
{
    private sealed record CacheEntry(BitmapSource? Icon);

    private const uint FileAttributeDirectory = 0x00000010;
    private const uint FileAttributeNormal = 0x00000080;
    private const uint ShgfiIcon = 0x00000100;
    private const uint ShgfiSmallIcon = 0x00000001;
    private const uint ShgfiUseFileAttributes = 0x00000010;

    private static readonly ConcurrentDictionary<string, CacheEntry> MemoryCache =
        new(StringComparer.OrdinalIgnoreCase);

    public static BitmapSource? GetIconSource(string? path)
    {
        var descriptor = BuildDescriptor(path);
        if (descriptor == null) return null;

        return MemoryCache.GetOrAdd(
            descriptor.Value.CacheKey,
            _ => new CacheEntry(LoadIconSource(descriptor.Value))).Icon;
    }

    internal static string? BuildCacheKey(string? path) => BuildDescriptor(path)?.CacheKey;

    private static IconDescriptor? BuildDescriptor(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;

        try
        {
            var trimmed = path.Trim();
            // 列表渲染期间不得访问条目指向的磁盘、映射盘或网络路径。
            // 明确带目录分隔符结尾的路径仍可显示文件夹图标；其余只按扩展名使用通用文件属性。
            if (Path.EndsInDirectorySeparator(trimmed))
                return new IconDescriptor("<folder>", "folder", FileAttributeDirectory);

            var extension = Path.GetExtension(trimmed);
            var normalizedExtension = string.IsNullOrEmpty(extension)
                ? "<file>"
                : extension.ToLowerInvariant();
            var shellPath = string.IsNullOrEmpty(extension) ? "file" : $"file{extension}";
            return new IconDescriptor(normalizedExtension, shellPath, FileAttributeNormal);
        }
        catch
        {
            return null;
        }
    }

    private static BitmapSource? LoadIconSource(IconDescriptor descriptor)
    {
        SHFILEINFO info = default;
        try
        {
            var result = SHGetFileInfo(
                descriptor.ShellPath,
                descriptor.FileAttributes,
                out info,
                (uint)Marshal.SizeOf<SHFILEINFO>(),
                ShgfiIcon | ShgfiSmallIcon | ShgfiUseFileAttributes);
            if (result == IntPtr.Zero || info.hIcon == IntPtr.Zero) return null;

            var bitmap = Imaging.CreateBitmapSourceFromHIcon(
                info.hIcon,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
            bitmap.Freeze();
            return bitmap;
        }
        catch
        {
            return null;
        }
        finally
        {
            if (info.hIcon != IntPtr.Zero)
                DestroyIcon(info.hIcon);
        }
    }

    private readonly record struct IconDescriptor(
        string CacheKey,
        string ShellPath,
        uint FileAttributes);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEINFO
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szDisplayName;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string szTypeName;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SHGetFileInfo(
        string pszPath,
        uint dwFileAttributes,
        out SHFILEINFO psfi,
        uint cbFileInfo,
        uint uFlags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr hIcon);
}
