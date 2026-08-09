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
    /// <summary>延时后打开跳转列表；与快捷键共用。</summary>
    /// <param name="armHotkeyDoubleTap">true 时参与「短时间内第二次快捷键直跳」探测。</param>
    /// <param name="autoForegroundStickyMode">true 时为自动前台模式：贴靠、不抢外部点击关窗、单击只导航。</param>
    private void ScheduleFileJumpPickerOpen(
        IntPtr dialogForPicker,
        List<FileJumpCandidate> capturedCandidates,
        int preferIdx,
        bool armHotkeyDoubleTap,
        bool allowShellInject,
        bool autoForegroundStickyMode,
        Action? afterPickerAssigned = null)
    {
        if (_appSettings == null) return;

        var tick = Environment.TickCount64;
        if (armHotkeyDoubleTap)
        {
            _fileJumpLastHotkeyTick = tick;
            _fileJumpLastDialogHwnd = dialogForPicker;
        }
        else
        {
            _fileJumpLastDialogHwnd = dialogForPicker;
        }

        var session = unchecked(++_fileJumpPickerSession);
        Win32.GetCursorPos(out var jumpMouseScreen);
        var jumpX = jumpMouseScreen.X;
        var jumpY = jumpMouseScreen.Y;

        CancelFileJumpPickerDelay();
        var delaySession = _fileJumpDelaySession;

        var delayMs = Math.Clamp(_appSettings.FileJumpPickerShowDelayMs, 0, 10000);

        void QueueOpenFileJumpPicker()
        {
            Dispatcher.BeginInvoke(() =>
            {
                // 若未真正打开列表窗，必须撤销双按窗口期，否则 _fileJumpLastHotkeyTick 仍有效，
                // 短时间内再按会误走「二次快捷键直跳」，表现为列表从不弹出。
                if (session != _fileJumpPickerSession)
                {
                    _fileJumpLastHotkeyTick = 0;
                    return;
                }

                if (_activeFileJumpPicker != null || _fileJumpPickerOpenInProgress)
                {
                    _fileJumpLastHotkeyTick = 0;
                    return;
                }

                _fileJumpLastHotkeyTick = 0;
                _fileJumpPickerOpenInProgress = true;
                FileDialogJumpPickerWindow? picker = null;
                var shown = false;
                try
                {
                    picker = new FileDialogJumpPickerWindow(
                        capturedCandidates, preferIdx, jumpX, jumpY, _appSettings!, dialogForPicker,
                        autoForegroundStickyMode);
                    _activeFileJumpPicker = picker;
                    picker.Closed += (_, _) =>
                    {
                        if (ReferenceEquals(_activeFileJumpPicker, picker))
                            _activeFileJumpPicker = null;
                        StopExplorerPathPoll();
                        ClearFileJumpDoubleTapState();
                    };
                    // ShowDialog 会开启嵌套消息循环；跳转窗本身已经通过全局钩子/Closed 维护生命周期，
                    // 使用 modeless Show 避免主 UI Dispatcher 被模态循环长期占住，减轻拖动和关闭延迟。
                    picker.Show();
                    shown = true;
                    if (afterPickerAssigned != null)
                    {
                        var pickerCapture = picker;
                        DispatcherTimer? fullCollectDelay = null;
                        fullCollectDelay = new DispatcherTimer
                        {
                            Interval = TimeSpan.FromMilliseconds(900)
                        };
                        fullCollectDelay.Tick += (_, _) =>
                        {
                            fullCollectDelay?.Stop();
                            fullCollectDelay = null;
                            if (session != _fileJumpPickerSession) return;
                            if (!ReferenceEquals(_activeFileJumpPicker, pickerCapture)) return;
                            if (!pickerCapture.IsLoaded) return;

                            try
                            {
                                afterPickerAssigned.Invoke();
                            }
                            catch (Exception ex)
                            {
                                ShellNavigateLog.Write("filejump", "afterPickerAssigned delayed: " + ex);
                            }
                        };
                        fullCollectDelay.Start();
                    }
                }
                catch (Exception ex)
                {
                    ShellNavigateLog.Write("filejump", "show jump picker: " + ex);
                    if (picker != null && ReferenceEquals(_activeFileJumpPicker, picker))
                        _activeFileJumpPicker = null;
                    StopExplorerPathPoll();
                }
                finally
                {
                    if (!shown && picker != null && ReferenceEquals(_activeFileJumpPicker, picker))
                    {
                        _activeFileJumpPicker = null;
                        StopExplorerPathPoll();
                    }
                    _fileJumpPickerOpenInProgress = false;
                }
            }, DispatcherPriority.Input);
        }

        if (delayMs <= 0)
        {
            QueueOpenFileJumpPicker();
            return;
        }

        _fileJumpOpenDelayTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(delayMs),
        };
        _fileJumpOpenDelayTimer.Tick += (_, _) =>
        {
            _fileJumpOpenDelayTimer?.Stop();
            _fileJumpOpenDelayTimer = null;
            if (delaySession != _fileJumpDelaySession) return;
            QueueOpenFileJumpPicker();
        };
        _fileJumpOpenDelayTimer.Start();
    }

    private void CancelFileJumpPickerDelay()
    {
        if (_fileJumpOpenDelayTimer != null)
        {
            _fileJumpOpenDelayTimer.Stop();
            _fileJumpOpenDelayTimer = null;
        }
        unchecked { _fileJumpDelaySession++; }
    }

    /// <summary>前台可能是跳转列表窗；此时仍应对背后文件对话框取路径、导航。</summary>
    private IntPtr ResolveFileJumpTargetHwndInternal(IntPtr fgNow)
    {
        var resolved = FileDialogJumpHelper.ResolveFileDialogHwndFromWindowOrAncestor(fgNow);
        if (resolved != IntPtr.Zero)
            return resolved;

        // 仅当前台就是上次对话框时才复用，避免后台残留对话框导致 Ctrl+G 误导航。
        if (_fileJumpLastDialogHwnd != IntPtr.Zero
            && _fileJumpLastDialogHwnd == fgNow
            && Win32.IsWindow(_fileJumpLastDialogHwnd)
            && FileDialogJumpHelper.ClassifyFileDialog(_fileJumpLastDialogHwnd) != FileDialogKind.None)
            return _fileJumpLastDialogHwnd;

        if (_fileJumpLastDialogHwnd != IntPtr.Zero
            && _fileJumpLastDialogHwnd == fgNow
            && Win32.IsWindow(_fileJumpLastDialogHwnd)
            && CustomFileDialogStore.FindMatchingRule(_fileJumpLastDialogHwnd) != null)
            return _fileJumpLastDialogHwnd;

        return IntPtr.Zero;
    }

    private static int PreferCandidateIndex(IntPtr dialogHwnd, List<FileJumpCandidate> candidates)
    {
        // 本方法运行在 WPF Dispatcher 上，只读现成缓存；完整采集稍后会在后台补齐。
        var zPath = FileManagerPathCollector.TryGetZOrderLinkedFolder(
            dialogHwnd,
            2,
            allowBlockingExplorerRefresh: false,
            allowStaleExplorerCache: true,
            allowExplorerUiAutomation: false,
            allowBlockingSpecializedManagers: false);
        if (string.IsNullOrEmpty(zPath)) return 0;
        var idx = candidates.FindIndex(c =>
            string.Equals(c.Path, zPath, StringComparison.OrdinalIgnoreCase));
        return idx >= 0 ? idx : 0;
    }

    private void ClearFileJumpDoubleTapState()
    {
        CancelFileJumpPickerDelay();
        _fileJumpLastHotkeyTick = 0;
        _fileJumpLastDialogHwnd = IntPtr.Zero;
    }

}
