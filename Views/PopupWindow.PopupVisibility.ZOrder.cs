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
    private void ApplyPendingPositionSetWindowPos()
    {
        _isOurSetWindowPos = true;
        try
        {
            Win32.SetWindowPos(_hwnd, IntPtr.Zero,
                _pendingPhysX, _pendingPhysY, 0, 0,
                Win32.SWP_NOSIZE | Win32.SWP_NOZORDER | Win32.SWP_NOACTIVATE);
        }
        finally
        {
            _isOurSetWindowPos = false;
        }
    }

    /// <summary>
    /// 在定位后重申 HWND_TOPMOST（单次），不抢焦点。
    /// </summary>
    private void ReassertPopupTopmostZOrder()
    {
        if (_hwnd == IntPtr.Zero) return;
        _isOurSetWindowPos = true;
        try
        {
            Win32.SetWindowPos(_hwnd, Win32.HWND_TOPMOST,
                0, 0, 0, 0,
                Win32.SWP_NOMOVE | Win32.SWP_NOSIZE | Win32.SWP_NOACTIVATE);
        }
        finally
        {
            _isOurSetWindowPos = false;
        }
    }

    /// <summary>
    /// 尽力将本窗口插在 Shell 根窗口之上并重申 TOPMOST。Win11 25H2 等版本可能将开始/搜索置于更高 Z 带，
    /// 此时用户态无法保证显示在最前。
    /// </summary>
    private void ApplyShellForegroundZOrderFix()
    {
        if (_hwnd == IntPtr.Zero) return;
        var fg = Win32.GetForegroundWindow();
        if (!IsShellForegroundWindow(fg) || fg == _hwnd) return;

        var root = Win32.GetAncestor(fg, Win32.GA_ROOT);
        var insertAfter = root != IntPtr.Zero ? root : fg;
        if (insertAfter == IntPtr.Zero || insertAfter == _hwnd) return;

        // 先进 TOPMOST 带，再插在 Shell 根窗口之上，避免只做相对 Z 序时被后续 TOPMOST 冲掉顺序。
        ReassertPopupTopmostZOrder();

        _isOurSetWindowPos = true;
        try
        {
            Win32.SetWindowPos(_hwnd, insertAfter, 0, 0, 0, 0,
                Win32.SWP_NOMOVE | Win32.SWP_NOSIZE | Win32.SWP_NOACTIVATE);
        }
        finally
        {
            _isOurSetWindowPos = false;
        }
    }
}
