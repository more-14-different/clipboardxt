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
    public void Cleanup()
    {
#if CLIPX_FILEJUMP
        UninstallFileJumpPersistFolderHook();
        DisarmFileJumpClickToNavigate();
        lock (_externalFolderResolveGate)
        {
            _externalFolderResolveStopping = true;
            _pendingDialogFolderResolvePrevious = IntPtr.Zero;
            _pendingExternalFolderResolvePrevious = IntPtr.Zero;
            _pendingExternalFolderResolveCurrent = IntPtr.Zero;
        }
        _fileJumpAutoFirstJumpDoneRoot = IntPtr.Zero;
        _fileJumpAutoOpenDebounceTimer?.Stop();
        _fileJumpAutoOpenDebounceTimer = null;
#endif
        UninstallKeyboardHook();
        UninstallMouseHook();
#if CLIPX_FILEJUMP
        UninstallForegroundWatcher();
#endif
#if CLIPX_CLIPBOARD
        Win32.UnregisterHotKey(_hwnd, HotkeyId);
#endif
#if CLIPX_FILEJUMP
        Win32.UnregisterHotKey(_hwnd, HotkeyJumpLastFolderId);
#endif
#if CLIPX_CLIPBOARD
        Win32.RemoveClipboardFormatListener(_hwnd);
#endif
    }
}
