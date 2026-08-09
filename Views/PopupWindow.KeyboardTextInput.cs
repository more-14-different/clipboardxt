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
    private static char? VkToChar(uint vkCode, uint scanCode)
    {
        var keyState = new byte[256];
        if ((Win32.GetAsyncKeyState(0x10) & 0x8000) != 0) { keyState[0x10] = 0x80; keyState[0xA0] = 0x80; }
        if ((Win32.GetKeyState(0x14) & 0x0001) != 0) keyState[0x14] = 0x01;

        var sb = new StringBuilder(4);
        int result = Win32.ToUnicode(vkCode, scanCode, keyState, sb, sb.Capacity, 0);
        if (result < 0) Win32.ToUnicode(vkCode, scanCode, keyState, sb, sb.Capacity, 0);
        if (result == 1 && !char.IsControl(sb[0])) return sb[0];
        return null;
    }
}
