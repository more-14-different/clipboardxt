using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Brush = System.Windows.Media.Brush;

namespace ClipboardManager;

public partial class ExplorerQuickFindWindow : Window
{
    public void ApplySettings(AppSettings settings)
    {
        _settings = settings;
        Width = settings.ExplorerQuickFindWidth;
        MaxHeight = settings.ExplorerQuickFindMaxHeight;
        if (settings.ExplorerQuickFindHeight > 0)
        {
            _userHasResized = true;
            SizeToContent = SizeToContent.Manual;
            Height = settings.ExplorerQuickFindHeight;
        }
    }

    private void SaveSize()
    {
        if (_settings == null) return;
        _settings.ExplorerQuickFindWidth = Width;
        _settings.ExplorerQuickFindMaxHeight = MaxHeight;
        if (_userHasResized && ActualHeight > 0)
            _settings.ExplorerQuickFindHeight = ActualHeight;
        _settings.Save();
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        switch (msg)
        {
            case Win32.WM_NCHITTEST:
                var htResult = WindowResizeHelper.HandleNcHitTest(_hwnd, lParam, 16, ref handled);
                if (handled) return htResult;
                break;

            case Win32.WM_SIZING:
                if (WindowResizeHelper.HandleWmSizing(_hwnd, wParam, lParam,
                    MinWidth > 0 ? MinWidth : 280, 1200,
                    MinHeight > 0 ? MinHeight : 200, MaxHeight > 0 ? MaxHeight : 900,
                    _userHasResized))
                {
                    _userHasResized = true;
                    Dispatcher.BeginInvoke(() => SizeToContent = SizeToContent.Manual);
                }
                var sizingRc = Marshal.PtrToStructure<Win32.RECT>(lParam);
                Dispatcher.BeginInvoke(() =>
                {
                    var src = HwndSource.FromHwnd(_hwnd);
                    double sx = src?.CompositionTarget != null ? src.CompositionTarget.TransformFromDevice.M11 : 1;
                    double sy = src?.CompositionTarget != null ? src.CompositionTarget.TransformFromDevice.M22 : 1;
                    double w = (sizingRc.Right - sizingRc.Left) * sx;
                    double h = (sizingRc.Bottom - sizingRc.Top) * sy;
                    if (w > 0 && h > 0)
                    {
                        Width = w;
                        Height = h;
                        MaxHeight = Math.Max(MaxHeight, h);
                    }
                });
                handled = true;
                return IntPtr.Zero;

            case Win32.WM_ENTERSIZEMOVE:
                break;

            case Win32.WM_EXITSIZEMOVE:
                SaveSize();
                break;
        }
        return IntPtr.Zero;
    }
}
