using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using ClipboardManager.Models;
using Media = System.Windows.Media;
using Orientation = System.Windows.Controls.Orientation;

namespace ClipboardManager;

public partial class FileDialogJumpPickerWindow : Window
{    private void CommitSelection(string path, bool pasteText = false)
    {
        ClearSearchText();
        // 所有模式统一走 CommitNavigateKeepOpen：
        //   pasteText=true（Ctrl+Enter/Ctrl+Click）→ 独立面板模式通过 callback 复用主面板完整粘贴，跟随文件对话框模式仅纯净复制到剪贴板
        //   pasteText=false + dlgHwnd==0（全局模式）→ 打开文件夹
        //   pasteText=false + dlgHwnd!=0（文件对话框模式）→ 导航到目标路径
        CommitNavigateKeepOpen(path, pasteText);
    }

    private static string NormalizeCommitNavigatePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return "";
        try
        {
            return Path.GetFullPath(path.Trim()).TrimEnd('\\', '/');
        }
        catch
        {
            return path.Trim().TrimEnd('\\', '/');
        }
    }

    /// <summary>
    /// 粘性模式下由外部触发：保持窗口打开，直接导航到目标路径并在完成后刷新列表。
    /// </summary>
    public void NavigateKeepOpenToPath(string path)
    {
        if (!_autoForegroundStickyMode) return;
        if (string.IsNullOrWhiteSpace(path)) return;
        SelectedPath = path;
        CommitNavigateKeepOpen(path);
    }

    private void CommitAndClose(string path)
    {
        SelectedPath = path;
        Close();
    }

    /// <summary>粘性自动模式：只切换文件对话框目录并刷新列表，不关闭窗口。</summary>
    private void CommitNavigateKeepOpen(string path, bool pasteText = false)
    {
        var dlgHwnd = _fileDialogOwnerHwnd;
        var normalizedPath = NormalizeCommitNavigatePath(path);
        if (!pasteText
            && dlgHwnd != IntPtr.Zero
            && _autoForegroundStickyMode
            && !string.IsNullOrEmpty(normalizedPath)
            && _commitNavigateKeepOpenUntilTick != 0
            && Environment.TickCount64 <= _commitNavigateKeepOpenUntilTick
            && string.Equals(_commitNavigateKeepOpenPath, normalizedPath, StringComparison.OrdinalIgnoreCase))
            return;

        if (!pasteText && dlgHwnd != IntPtr.Zero && _autoForegroundStickyMode)
        {
            _commitNavigateKeepOpenPath = normalizedPath;
            _commitNavigateKeepOpenUntilTick = Environment.TickCount64 + 1500;
        }
        unchecked { _commitNavigateKeepOpenGen++; }
        var gen = _commitNavigateKeepOpenGen;

        if (pasteText)
        {
            // 纯文本粘贴不要求路径存在；MRU 记录同样不得访问文件系统。
            _settings.RecordRecentFolderUse(path);
            _suppressJumpHook = true;

            // 1. 隐藏面板，让 UI 立即恢复响应，消除鼠标沙漏卡顿
            Hide();

            if (_standalonePasteCallback != null && _standaloneTargetHwnd != IntPtr.Zero)
            {
                _ = _standalonePasteCallback(path, _standaloneTargetHwnd);
                Dispatcher.BeginInvoke(new Action(Close), System.Windows.Threading.DispatcherPriority.Background);
                return;
            }

            // 2. 将易阻塞的剪贴板写入转移到后台 STA 线程 (Fallback)
            var pasteThread = new System.Threading.Thread(() =>
            {
                for (int i = 0; i < 5; i++)
                {
                    try
                    {
                        System.Windows.Clipboard.SetText(path);
                        break;
                    }
                    catch { System.Threading.Thread.Sleep(40); }
                }

                Dispatcher.BeginInvoke(new Action(Close), System.Windows.Threading.DispatcherPriority.Background);
            })
            {
                IsBackground = true,
                Name = "FileJumpStandalonePasteThread"
            };
            pasteThread.SetApartmentState(System.Threading.ApartmentState.STA);
            pasteThread.Start();

            return;
        }

        // 全局模式（无文件对话框）：直接在资源管理器中打开文件夹
        if (dlgHwnd == IntPtr.Zero)
        {
            try
            {
                var existingPath = Path.GetFullPath(path.Trim());
                if (!Directory.Exists(existingPath)) return;
                Process.Start(new ProcessStartInfo
                {
                    FileName = existingPath,
                    UseShellExecute = true,
                    Verb = "open"
                });
                _settings.RecordRecentFolderUse(existingPath);
            }
            catch { /* ignore */ }
            Close();
            return;
        }
        var allowInject = _settings.EnableShellNavigateInject;
        var memBefore = _settings.LastFileDialogFolder?.Trim();

        // 导航过程会通过 SendInput 向目标对话框发送键盘事件（Alt+N / Ctrl+A / 路径 / Enter）；
        // 低级键盘钩子仍在运行，会拦截 Enter 并再次 CommitSelection → 死循环。
        // 在导航+采集完成前抑制钩子，让按键直接传递到目标对话框。
        _suppressJumpHook = true;

        void StaWork()
        {
            try
            {
                if (!FileDialogJumpHelper.TryNavigateToFolder(dlgHwnd, path, allowInject))
                    return;

                string? folderAfter = null;
                try
                {
                    if (FileDialogJumpHelper.TryReadCurrentFolder(dlgHwnd, out var folder)
                        && !string.IsNullOrEmpty(folder))
                        folderAfter = folder;
                }
                catch { /* ignore */ }

                var memForCollect = !string.IsNullOrEmpty(folderAfter?.Trim())
                    ? folderAfter.Trim()
                    : memBefore;

                List<FileJumpCandidate> fresh;
                try
                {
                    fresh = FileManagerPathCollector.CollectCandidates(dlgHwnd, memForCollect,
                        recentFolders: _settings.RecentFileDialogFolders);
                }
                catch
                {
                    // 与原先一致：采集失败则不刷新列表，但仍尽量写入当前目录记忆。
                    var folderOnly = !string.IsNullOrEmpty(folderAfter) ? folderAfter : path;
                    Dispatcher.BeginInvoke(() =>
                    {
                        if (gen != _commitNavigateKeepOpenGen) return;
                        try
                        {
                            if (!string.IsNullOrEmpty(folderOnly))
                                _settings.RecordRecentFolderUse(folderOnly);
                        }
                        catch { /* ignore */ }
                    }, DispatcherPriority.Normal);
                    return;
                }

                var folderForSettings = !string.IsNullOrEmpty(folderAfter) ? folderAfter : path;
                Dispatcher.BeginInvoke(() =>
                {
                    if (gen != _commitNavigateKeepOpenGen) return;
                    try
                    {
                        if (!string.IsNullOrEmpty(folderForSettings))
                            _settings.RecordRecentFolderUse(folderForSettings);
                    }
                    catch { /* ignore */ }

                    ApplyNavigateKeepOpenListRefresh(path, fresh);
                }, DispatcherPriority.Normal);
            }
            finally
            {
                Dispatcher.BeginInvoke(() =>
                {
                    if (gen == _commitNavigateKeepOpenGen)
                        _suppressJumpHook = false;
                }, DispatcherPriority.Normal);
            }
        }

        var th = new Thread(StaWork)
        {
            IsBackground = true,
            Name = "ClipboardX-JumpPicker-NavigateRefresh",
        };
        th.SetApartmentState(ApartmentState.STA);
        th.Start();
    }

}

