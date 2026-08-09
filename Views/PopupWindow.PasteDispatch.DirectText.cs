using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Brush = System.Windows.Media.Brush;
using Button = System.Windows.Controls.Button;
using Orientation = System.Windows.Controls.Orientation;
using TextBox = System.Windows.Controls.TextBox;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using ClipboardManager.Models;

namespace ClipboardManager;

public partial class PopupWindow : Window
{
    /// <summary>文本回退路径：完全绕过系统剪贴板，直接向目标窗口注入 Unicode 按键。</summary>
    private static bool TryDirectTypeTextToTarget(string text)
    {
        if (string.IsNullOrEmpty(text))
            return false;

        if (ReleaseHeldModifiers())
        {
            Thread.Sleep(1);
        }

        const int chunkSize = 256;
        for (var i = 0; i < text.Length; i += chunkSize)
        {
            var chunk = text.Substring(i, Math.Min(chunkSize, text.Length - i));
            if (!SendUnicodeString(chunk))
                return false;
        }

        return true;
    }

    private static bool TryDirectInsertTextToFocusedEditControl(IntPtr targetWindow, string text)
    {
        if (targetWindow == IntPtr.Zero || string.IsNullOrEmpty(text) || !Win32.IsWindow(targetWindow))
            return false;

        var fgThread = Win32.GetWindowThreadProcessId(targetWindow, out _);
        if (fgThread == 0) return false;
        var curThread = Win32.GetCurrentThreadId();
        var attached = false;
        try
        {
            if (fgThread != curThread)
                attached = Win32.AttachThreadInput(curThread, fgThread, true);

            var focus = Win32.GetFocus();
            if (focus == IntPtr.Zero || !Win32.IsWindow(focus))
                return false;

            var cls = Win32.GetWindowClassName(focus);
            if (!IsEditableClass(cls))
                return false;

            var style = Win32.GetWindowLongPtr(focus, Win32.GWL_STYLE).ToInt64();
            if ((style & Win32.ES_READONLY) != 0)
                return false;

            _ = Win32.SendMessage(focus, Win32.EM_REPLACESEL, new IntPtr(1), text);
            return true;
        }
        catch
        {
            return false;
        }
        finally
        {
            if (attached)
                Win32.AttachThreadInput(curThread, fgThread, false);
        }
    }

    private static bool IsEditableClass(string cls)
    {
        if (string.IsNullOrEmpty(cls)) return false;
        return cls.Equals("Edit", StringComparison.OrdinalIgnoreCase)
               || cls.StartsWith("RICHEDIT", StringComparison.OrdinalIgnoreCase)
               || cls.StartsWith("WindowsForms10.EDIT", StringComparison.OrdinalIgnoreCase);
    }
}
