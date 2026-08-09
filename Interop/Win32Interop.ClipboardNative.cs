using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace ClipboardManager;

internal static partial class Win32
{
    public const uint CF_UNICODETEXT = 13;
    public const uint CF_DIB = 8;
    public const uint CF_HDROP = 15;
    public const uint GMEM_MOVEABLE = 0x0002;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalAlloc(uint flags, UIntPtr bytes);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalLock(IntPtr memory);

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalUnlock(IntPtr memory);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalFree(IntPtr memory);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetClipboardData(uint format, IntPtr memory);

    public static string DescribeClipboardHolder()
    {
        var hwnd = GetOpenClipboardWindow();
        if (hwnd == IntPtr.Zero) return "none";
        GetWindowThreadProcessId(hwnd, out var pid);
        try
        {
            return $"hwnd=0x{hwnd.ToInt64():X} pid={pid} name={Process.GetProcessById((int)pid).ProcessName}";
        }
        catch
        {
            return $"hwnd=0x{hwnd.ToInt64():X} pid={pid} name=?";
        }
    }

    public static bool TrySetClipboardTextNative(string text, IntPtr owner) =>
        TrySetClipboardMemory(CF_UNICODETEXT, Encoding.Unicode.GetBytes(text + '\0'), owner);

    public static bool TrySetClipboardDibNative(byte[] dib, IntPtr owner) =>
        dib.Length >= 40 && TrySetClipboardMemory(CF_DIB, dib, owner);

    public static bool TrySetClipboardFileDropListNative(IReadOnlyList<string> paths, IntPtr owner)
    {
        const int dropFilesSize = 20;
        var joined = string.Join('\0', paths.Where(p => !string.IsNullOrWhiteSpace(p))) + "\0\0";
        if (joined.Length == 2) return false;
        var pathBytes = Encoding.Unicode.GetBytes(joined);
        var payload = new byte[dropFilesSize + pathBytes.Length];
        BitConverter.GetBytes(dropFilesSize).CopyTo(payload, 0);
        BitConverter.GetBytes(1).CopyTo(payload, 16);
        pathBytes.CopyTo(payload, dropFilesSize);
        return TrySetClipboardMemory(CF_HDROP, payload, owner);
    }

    private static bool TrySetClipboardMemory(uint format, byte[] payload, IntPtr owner)
    {
        if (owner == IntPtr.Zero || payload.Length == 0 || !OpenClipboard(owner)) return false;
        IntPtr memory = IntPtr.Zero;
        try
        {
            if (!EmptyClipboard()) return false;
            memory = GlobalAlloc(GMEM_MOVEABLE, (UIntPtr)payload.Length);
            if (memory == IntPtr.Zero) return false;
            var destination = GlobalLock(memory);
            if (destination == IntPtr.Zero) return false;
            try
            {
                Marshal.Copy(payload, 0, destination, payload.Length);
            }
            finally
            {
                GlobalUnlock(memory);
            }

            if (SetClipboardData(format, memory) == IntPtr.Zero) return false;
            memory = IntPtr.Zero;
            return true;
        }
        finally
        {
            if (memory != IntPtr.Zero) GlobalFree(memory);
            CloseClipboard();
        }
    }
}
