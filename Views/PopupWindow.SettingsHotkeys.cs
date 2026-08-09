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
#if CLIPX_CLIPBOARD
    public bool UpdateHotkey(uint modifiers, uint key)
    {
        Win32.UnregisterHotKey(_hwnd, HotkeyId);
        if (!Win32.RegisterHotKey(_hwnd, HotkeyId, modifiers | Win32.MOD_NOREPEAT, key))
        {
            Win32.RegisterHotKey(_hwnd, HotkeyId, _hotkeyModifiers | Win32.MOD_NOREPEAT, _hotkeyKey);
            return false;
        }
        _hotkeyModifiers = modifiers;
        _hotkeyKey = key;
        return true;
    }
#else
    public bool UpdateHotkey(uint modifiers, uint key) => true;
#endif

    public bool UpdateFileJumpHotkey(uint modifiers, uint key)
    {
        Win32.UnregisterHotKey(_hwnd, HotkeyJumpLastFolderId);
        if (!Win32.RegisterHotKey(_hwnd, HotkeyJumpLastFolderId, modifiers | Win32.MOD_NOREPEAT, key))
        {
            Win32.RegisterHotKey(_hwnd, HotkeyJumpLastFolderId,
                _fileJumpHotkeyModifiers | Win32.MOD_NOREPEAT, _fileJumpHotkeyKey);
            return false;
        }
        _fileJumpHotkeyModifiers = modifiers;
        _fileJumpHotkeyKey = key;
        return true;
    }
}
