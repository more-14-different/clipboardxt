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

/// <summary>
/// 资源管理器内打字触发 Everything 当前文件夹上下文检索。
/// 架构约束：WH_KEYBOARD_LL 回调 < 1ms，所有 COM/UIA/UI 操作走 BeginInvoke。
/// </summary>
public sealed partial class ExplorerQuickFindController : IDisposable
{
    private readonly Dispatcher _dispatcher;
    private readonly AppSettings _settings;
    private IntPtr _hook;
    private static readonly Win32.LowLevelKeyboardProc Thunk = StaticHookProc;
    private static ExplorerQuickFindController? s_owner;

    // ---- 会话状态（仅 Dispatcher 线程读写） ----
    private ExplorerQuickFindWindow? _window;
    private bool _session;
    private IntPtr _sessionExplorerFrame;
    private string _sessionFolderPath = "";
    private string _sessionFolderDisplay = "";
    private string _typing = "";
    private int _queryGen;
    private CancellationTokenSource? _queryCts;

    // ---- 钩回调线程快速判断用（原子读写） ----
    private volatile bool _sessionActive;

    // ---- 异步初始化期间缓冲（BeginSessionAsync 完成前到达的字符） ----
    private readonly List<char> _pendingChars = new();

    public ExplorerQuickFindController(Dispatcher dispatcher, AppSettings settings)
    {
        _dispatcher = dispatcher;
        _settings = settings;
    }

    public void Start()
    {
        if (!_settings.ExplorerEverythingQuickFindEnabled) return;
        if (_hook != IntPtr.Zero) return;
        s_owner = this;
        _hook = Win32.SetWindowsHookEx(Win32.WH_KEYBOARD_LL, Thunk, Win32.GetModuleHandle(null), 0);
        if (_hook == IntPtr.Zero)
        {
            var err = Marshal.GetLastWin32Error();
            LogDiag($"SetWindowsHookEx(WH_KEYBOARD_LL) 失败，Win32={err}");
            TryAppendLog($"键盘钩安装失败 Win32={err}");
            s_owner = null;
        }
        else
        {
            LogDiag("SetWindowsHookEx(WH_KEYBOARD_LL) 成功");
            TryAppendLog("键盘钩已安装");
        }
    }

    public void Dispose()
    {
        if (_hook != IntPtr.Zero)
        {
            Win32.UnhookWindowsHookEx(_hook);
            _hook = IntPtr.Zero;
        }
        if (s_owner == this) s_owner = null;

        _queryCts?.Cancel();

        void Cleanup()
        {
            ResetSessionState();
            if (_window != null)
            {
                var w = _window;
                _window = null;
                w.UserClosed -= OnWindowClosed;
                w.ItemActivated -= OnItemActivated;
                try { w.Close(); } catch { }
            }
        }

        if (_dispatcher.CheckAccess())
            Cleanup();
        else
            _dispatcher.Invoke(Cleanup);
    }

}
