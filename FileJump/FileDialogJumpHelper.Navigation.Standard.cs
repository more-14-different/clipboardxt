using System.IO;
using System.Text;
using System.Windows.Automation;

namespace ClipboardManager;

internal static partial class FileDialogJumpHelper
{
    private static void ActivateDialog(IntPtr dialogHwnd)
    {
        var dialogThreadId = Win32.GetWindowThreadProcessId(dialogHwnd, out _);
        var currentThreadId = Win32.GetCurrentThreadId();
        Win32.AttachThreadInput(currentThreadId, dialogThreadId, true);
        try { Win32.SetForegroundWindow(dialogHwnd); }
        finally { Win32.AttachThreadInput(currentThreadId, dialogThreadId, false); }
    }

    /// <summary>聚焦 ReBarWindow32 后用 F4 打开地址输入框，再填入路径。</summary>
    private static bool TryNavigateReBarF4AddressEdit(
        IntPtr dialogHwnd,
        string folderWithSlash,
        IntPtr skipEditHwnd)
    {
        var rebar = FindFirstHwndByClass(dialogHwnd, "ReBarWindow32");
        if (rebar == IntPtr.Zero) return false;

        var dialogThreadId = Win32.GetWindowThreadProcessId(dialogHwnd, out _);
        var currentThreadId = Win32.GetCurrentThreadId();

        Win32.AttachThreadInput(currentThreadId, dialogThreadId, true);
        try
        {
            Win32.SetForegroundWindow(dialogHwnd);
            Thread.Sleep(50);
            Win32.SetFocus(rebar);
            Thread.Sleep(40);
            SendF4();
            Thread.Sleep(100);

            for (var i = 0; i < 45; i++)
            {
                var focused = Win32.GetFocus();
                if (focused != IntPtr.Zero
                    && focused != skipEditHwnd
                    && IsWin32PathInputClass(Win32.GetWindowClassName(focused)))
                {
                    if (TryWpsFillEditWithFolderAndEnter(focused, folderWithSlash))
                        return true;
                }
                Thread.Sleep(15);
            }
        }
        finally
        {
            Win32.AttachThreadInput(currentThreadId, dialogThreadId, false);
        }

        return false;
    }

    private static IntPtr FindFirstHwndByClass(IntPtr root, string className)
    {
        var found = IntPtr.Zero;
        void Walk(IntPtr window)
        {
            if (found != IntPtr.Zero) return;
            if (string.Equals(
                    Win32.GetWindowClassName(window),
                    className,
                    StringComparison.OrdinalIgnoreCase))
            {
                found = window;
                return;
            }
            Win32.EnumChildWindows(window, (child, _) =>
            {
                Walk(child);
                return true;
            }, IntPtr.Zero);
        }
        Walk(root);
        return found;
    }

    /// <summary>取屏幕上最靠下的路径类输入控件，通常为文件名或底部路径框。</summary>
    private static IntPtr FindBottomMostPathInputHwnd(IntPtr root)
    {
        var best = IntPtr.Zero;
        var bestBottom = int.MinValue;
        void Walk(IntPtr window)
        {
            var windowClass = Win32.GetWindowClassName(window);
            if (IsWin32PathInputClass(windowClass)
                && Win32.GetWindowRect(window, out var rectangle)
                && rectangle.Bottom >= bestBottom)
            {
                bestBottom = rectangle.Bottom;
                best = window;
            }
            Win32.EnumChildWindows(window, (child, _) =>
            {
                Walk(child);
                return true;
            }, IntPtr.Zero);
        }
        Walk(root);
        return best;
    }

    private static bool IsWin32PathInputClass(string windowClass)
    {
        if (string.Equals(windowClass, "Edit", StringComparison.OrdinalIgnoreCase)) return true;
        if (windowClass.StartsWith("RichEdit", StringComparison.OrdinalIgnoreCase)) return true;
        return string.Equals(windowClass, "RICHEDIT50W", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryNavigateSysListViewStyle(IntPtr dialogHwnd, string folderPath)
    {
        if (!Directory.Exists(folderPath)) return false;
        var normalized = Path.GetFullPath(folderPath);
        var folderWithSlash = normalized.TrimEnd('\\') + "\\";

        var edit = FindFirstEditControl(dialogHwnd);
        if (edit == IntPtr.Zero) return false;

        ActivateDialog(dialogHwnd);
        Thread.Sleep(40);

        var capacity = (int)(long)Win32.SendMessage(
            edit,
            Win32.WM_GETTEXTLENGTH,
            IntPtr.Zero,
            IntPtr.Zero);
        if (capacity < 0) capacity = 0;
        capacity = Math.Min(capacity + 64, 32767);
        var oldTextBuffer = new StringBuilder(Math.Max(capacity, 256));
        Win32.SendMessage(edit, Win32.WM_GETTEXT, (IntPtr)oldTextBuffer.Capacity, oldTextBuffer);
        var oldText = oldTextBuffer.ToString();

        Win32.SendMessage(edit, Win32.WM_SETTEXT, IntPtr.Zero, folderWithSlash);
        Win32.SetFocus(edit);
        Thread.Sleep(30);
        SendEnter();
        Thread.Sleep(80);

        Win32.SendMessage(edit, Win32.WM_SETTEXT, IntPtr.Zero, oldText);
        return true;
    }

    private static IntPtr FindFirstEditControl(IntPtr root)
    {
        var found = IntPtr.Zero;
        void Walk(IntPtr window)
        {
            if (found != IntPtr.Zero) return;
            if (string.Equals(Win32.GetWindowClassName(window), "Edit", StringComparison.Ordinal))
            {
                found = window;
                return;
            }
            Win32.EnumChildWindows(window, (child, _) =>
            {
                Walk(child);
                return true;
            }, IntPtr.Zero);
        }
        Walk(root);
        return found;
    }

    private static bool TryNavigateAddressBarStyle(IntPtr dialogHwnd, string folderPath)
    {
        if (!Directory.Exists(folderPath)) return false;
        var normalized = Path.GetFullPath(folderPath);

        ActivateDialog(dialogHwnd);
        Thread.Sleep(60);

        var folderWithSlash = normalized.TrimEnd('\\', '/') + "\\";
        var bottomEdit = FindBottomMostPathInputHwnd(dialogHwnd);
        if (TryNavigateReBarF4AddressEdit(dialogHwnd, folderWithSlash, bottomEdit))
            return true;

        SendCtrlL();
        Thread.Sleep(140);

        if (TrySetFocusedAddressValue(normalized))
        {
            Thread.Sleep(40);
            SendEnter();
            return true;
        }

        SendCtrlA();
        Thread.Sleep(30);
        SendUnicodeString(normalized);
        Thread.Sleep(30);
        SendEnter();
        return true;
    }

    private static bool TrySetFocusedAddressValue(string path)
    {
        try
        {
            var focused = AutomationElement.FocusedElement;
            if (focused == null) return false;
            if (focused.TryGetCurrentPattern(ValuePattern.Pattern, out var pattern))
            {
                ((ValuePattern)pattern).SetValue(path);
                return true;
            }
        }
        catch { }
        return false;
    }
}
