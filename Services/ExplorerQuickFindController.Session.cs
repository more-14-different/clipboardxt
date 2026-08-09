using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace ClipboardManager;

public sealed partial class ExplorerQuickFindController : IDisposable
{    private async void BeginSessionAsync(IntPtr frame, char firstChar, bool isDesktop = false)
    {
        string? folder = null;

        if (isDesktop)
        {
            folder = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            LogDiag($"桌面场景，使用桌面目录 folder={folder}");
        }
        else
        {
            try
            {
                folder = await Task.Run(() => FileManagerPathCollector.TryGetExplorerFolderIfForeground(frame));
            }
            catch { /* ignore */ }
        }

        _session = true;
        _sessionExplorerFrame = frame;
        if (string.IsNullOrEmpty(folder))
        {
            _sessionFolderPath = "";
            _sessionFolderDisplay = "全盘";
            LogDiag($"非常规文件夹，全盘搜索 frame=0x{frame:X}");
        }
        else
        {
            try { _sessionFolderPath = Path.GetFullPath(folder.Trim()); }
            catch { _sessionFolderPath = folder; }
            _sessionFolderDisplay = _sessionFolderPath;
        }
        var rememberedQuery = _settings.RememberPanelOperationState
            ? _settings.PanelOperationStates.ExplorerQuickFindQuery
            : "";
        _typing = rememberedQuery + firstChar;

        // 消费异步初始化期间缓冲的字符
        if (_pendingChars.Count > 0)
        {
            foreach (var c in _pendingChars)
                _typing += c;
            _pendingChars.Clear();
        }

        EnsureWindow();
        _window!.SetQueryText(_sessionFolderDisplay, _typing, TypingHighlightNeedle(_typing));
        _window!.PositionNearExplorer(_sessionExplorerFrame);
        ScheduleQuery();
        LogDiag($"会话已启动 folder={_sessionFolderPath} typing={_typing}");
    }

    private void ProcessSessionKey(uint vk)
    {
        if (!_session)
        {
            if (_sessionActive && vk is Win32.VK_ESCAPE or Win32.VK_BACK)
            {
                _sessionActive = false;
                _sessionExplorerFrame = IntPtr.Zero;
                _pendingChars.Clear();
            }
            return;
        }

        switch (vk)
        {
            case Win32.VK_ESCAPE:
                EndSession();
                return;

            case Win32.VK_RETURN:
                var path = _window?.GetSelectedFullPath();
                var frame = _sessionExplorerFrame;
                if (string.IsNullOrEmpty(path))
                    return;
                EndSession(rememberQuery: false);
                _ = Task.Run(() => NavigateAndSelect(frame, path!, _settings.ExplorerQuickFindOpenMode));
                return;

            case Win32.VK_UP:
                _window?.MoveSelection(-1);
                return;

            case Win32.VK_DOWN:
                _window?.MoveSelection(1);
                return;

            case Win32.VK_LEFT:
                _window?.MoveSelectionPage(-1);
                return;

            case Win32.VK_RIGHT:
                _window?.MoveSelectionPage(1);
                return;

            case 0x21: // Page Up
                _window?.MoveSelectionPage(-1);
                return;

            case 0x22: // Page Down
                _window?.MoveSelectionPage(1);
                return;

            case 0x24: // Home
                _window?.MoveSelectionToEnd(false);
                return;

            case 0x23: // End
                _window?.MoveSelectionToEnd(true);
                return;

            case Win32.VK_DELETE:
                return;

            case Win32.VK_BACK:
                if (_typing.Length > 0)
                {
                    _typing = _typing[..^1];
                    if (_typing.Length == 0)
                    {
                        EndSession();
                        return;
                    }
                    _window?.SetQueryText(_sessionFolderDisplay, _typing, TypingHighlightNeedle(_typing));
                    ScheduleQuery();
                }
                else
                {
                    EndSession();
                }
                return;
        }
    }

    private static string? TypingHighlightNeedle(string? typing) =>
        string.IsNullOrWhiteSpace(typing) ? null : typing.Trim();

    private void AppendChar(char ch)
    {
        if (!_session)
        {
            // 异步初始化期间到达的字符先缓冲
            if (_sessionActive)
                _pendingChars.Add(ch);
            return;
        }
        _typing += ch;
        _window?.SetQueryText(_sessionFolderDisplay, _typing, TypingHighlightNeedle(_typing));
        ScheduleQuery();
    }

    // ===================== 会话管理 =====================

    private void EnsureWindow()
    {
        if (_window != null)
        {
            if (!_window.IsVisible)
                _window.Show();
            return;
        }
        _window = new ExplorerQuickFindWindow();
        _window.ApplySettings(_settings);
        _window.UserClosed += OnWindowClosed;
        _window.ItemActivated += OnItemActivated;
        _window.Show();
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        if (sender is ExplorerQuickFindWindow w)
        {
            w.UserClosed -= OnWindowClosed;
            w.ItemActivated -= OnItemActivated;
            if (_window == w)
                _window = null;
        }
        RememberCurrentOperationState();
        ResetSessionState();
    }

    private void OnItemActivated(string fullPath)
    {
        if (string.IsNullOrEmpty(fullPath)) return;
        var frame = _sessionExplorerFrame;
        EndSession(rememberQuery: false);
        _ = Task.Run(() => NavigateAndSelect(frame, fullPath, _settings.ExplorerQuickFindOpenMode));
    }

    private void QuickSelectAndActivate(int index)
    {
        if (!_session || _window == null) return;
        var path = _window.GetFullPathByIndex(index);
        if (string.IsNullOrEmpty(path)) return;
        var frame = _sessionExplorerFrame;
        EndSession(rememberQuery: false);
        _ = Task.Run(() => NavigateAndSelect(frame, path!, _settings.ExplorerQuickFindOpenMode));
    }

    private void EndSession() => EndSession(rememberQuery: true);

    private void EndSession(bool rememberQuery)
    {
        if (rememberQuery)
            RememberCurrentOperationState();
        else
            _settings.PanelOperationStates.ExplorerQuickFindQuery = "";
        ResetSessionState();
        _window?.Hide();
    }

    private void RememberCurrentOperationState()
    {
        _settings.PanelOperationStates.ExplorerQuickFindQuery =
            _settings.RememberPanelOperationState ? _typing : "";
    }

    private void ResetSessionState()
    {
        _session = false;
        _sessionActive = false;
        _sessionExplorerFrame = IntPtr.Zero;
        _sessionFolderPath = "";
        _sessionFolderDisplay = "";
        _typing = "";
        _pendingChars.Clear();
        _queryGen++;
        _queryCts?.Cancel();
        _queryCts = null;
    }

    // ===================== Everything 查询 =====================
}

