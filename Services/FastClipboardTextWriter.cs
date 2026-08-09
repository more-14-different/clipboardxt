using System.Runtime.InteropServices;
using System.Text;

namespace ClipboardManager;

internal static class FastClipboardTextWriter
{
    private const uint GmemMoveable = 0x0002;
    private const uint CfUnicodeText = 13;

    public static async Task<bool> TrySetAsync(
        IntPtr ownerHwnd,
        string text,
        int maxAttempts = 5,
        int retryDelayMs = 25)
    {
        if (string.IsNullOrEmpty(text)) return false;

        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            if (TrySetOnce(ownerHwnd, text)) return true;
            if (attempt + 1 < maxAttempts)
                await Task.Delay(retryDelayMs);
        }

        return false;
    }

    private static bool TrySetOnce(IntPtr ownerHwnd, string text)
    {
        var bytes = Encoding.Unicode.GetBytes(text + '\0');
        var memory = GlobalAlloc(GmemMoveable, (UIntPtr)bytes.Length);
        if (memory == IntPtr.Zero) return false;

        var clipboardOpened = false;
        var ownershipTransferred = false;
        try
        {
            var destination = GlobalLock(memory);
            if (destination == IntPtr.Zero) return false;
            try
            {
                Marshal.Copy(bytes, 0, destination, bytes.Length);
            }
            finally
            {
                GlobalUnlock(memory);
            }

            clipboardOpened = Win32.OpenClipboard(ownerHwnd);
            if (!clipboardOpened || !Win32.EmptyClipboard()) return false;

            ownershipTransferred = SetClipboardData(CfUnicodeText, memory) != IntPtr.Zero;
            return ownershipTransferred;
        }
        finally
        {
            if (clipboardOpened)
                Win32.CloseClipboard();
            if (!ownershipTransferred)
                GlobalFree(memory);
        }
    }

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
}
