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
    #region Window Message Hook

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
                _isResizing = true;
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
                _isResizing = true;
                break;

            case Win32.WM_EXITSIZEMOVE:
                _isResizing = false;
                SavePopupSize();
                break;

            case Win32.WM_MOUSEACTIVATE:
                handled = true;
                return TextEntryEditPopup.IsOpen
                    ? new IntPtr(Win32.MA_ACTIVATE)
                    : new IntPtr(Win32.MA_NOACTIVATE);

            case Win32.WM_DPICHANGED:
                // 允许紧随其后的 WINDOWPOSCHANGING 带上位置/尺寸，否则会与 WM_DPICHANGED 建议矩形冲突，跨屏缩放时表现为突然缩放、位置漂移。
                _windowPosNomoveSkipCount = 8;
                if (_isDragging)
                {
                    Win32.GetCursorPos(out _dragLastPt);
                    #region agent log
                    try
                    {
                        Win32.RECT suggested = default;
                        if (lParam != IntPtr.Zero)
                            suggested = Marshal.PtrToStructure<Win32.RECT>(lParam);
                        Win32.GetWindowRect(hwnd, out var winRc);
                        AgentDbgLog("H10", "WndProc WM_DPICHANGED", "drag: suppress default; suggested vs hwnd rect",
                            new
                            {
                                dpiWParam = wParam.ToInt64(),
                                suggested = new { suggested.Left, suggested.Top, suggested.Right, suggested.Bottom },
                                winRect = new { winRc.Left, winRc.Top, winRc.Right, winRc.Bottom }
                            });
                    }
                    catch
                    {
                        /* 调试日志 */
                    }
                    #endregion
                    // 主屏右缘前约一窗宽内窗口会「跨屏」，系统发 WM_DPICHANGED 且 lParam 为建议矩形；WPF/DefWindowProc 若应用该矩形会与
                    // 鼠标钩里的 SetWindowPos 抢位置，表现为危险区内跳变。拖动中吞掉本消息，松手后 Sync 再对齐 DPI/布局。
                    handled = true;
                    return IntPtr.Zero;
                }

                Dispatcher.BeginInvoke(DispatcherPriority.Background,
                    new Action(() => SyncWindowPhysicalPositionToWpf("wmDpiChanged")));
                break;

            case Win32.WM_WINDOWPOSCHANGING:
                // WM_DPICHANGED 之后系统会连发若干 WINDOWPOSCHANGING；此间若强行 SWP_NOMOVE，跨屏拖动时壳/DWM 无法按新 DPI 微调位置，
                // 会与鼠标钩 SetWindowPos 拉锯，表现为跨越边界时乱跳；松手后反而正常。故在计数窗口内一律不拦（与是否拖动无关）。
                if (_windowPosNomoveSkipCount > 0 && !_isOurSetWindowPos)
                {
                    _windowPosNomoveSkipCount--;
                    #region agent log
                    if (_isDragging && _agentDbgH17WpcSkipLogCount < 32)
                    {
                        _agentDbgH17WpcSkipLogCount++;
                        AgentDbgLog("H17", "WndProc WM_WINDOWPOSCHANGING", "skip NOMOVE (DPI chain)",
                            new { remainingAfter = _windowPosNomoveSkipCount, our = _isOurSetWindowPos });
                    }
                    #endregion
                    break;
                }

                // 外部发起的移动：弹窗常态锁位置；拖动时仅由钩子 SetWindowPos，禁止系统再改 x/y（尺寸仍可随 DPI 变）。
                // resize 时不能锁位置，否则拖左边缘时右边缘不动的约束会失效。
                if (!_isOurSetWindowPos && !_isResizing && (_isDragging || _isPopupVisible || _lockPopupWindowNomove))
                {
                    var pos = Marshal.PtrToStructure<Win32.WINDOWPOS>(lParam);
                    pos.flags |= Win32.SWP_NOMOVE;
                    Marshal.StructureToPtr(pos, lParam, false);
                }
                break;

#if CLIPX_CLIPBOARD
            case Win32.WM_CLIPBOARDUPDATE:
                OnClipboardUpdate();
                handled = true;
                break;
#endif
            case Win32.WM_HOTKEY:
                if (IsForegroundAppExcluded(_appSettings)) break;
                switch (wParam.ToInt32())
                {
#if CLIPX_CLIPBOARD
                    case HotkeyId:
                        TogglePopup();
                        handled = true;
                        break;
#endif
                    case HotkeyJumpLastFolderId:
                        TryJumpFileDialogToLastFolder();
                        handled = true;
                        break;
                }
                break;
        }
        return IntPtr.Zero;
    }

    #endregion
}

