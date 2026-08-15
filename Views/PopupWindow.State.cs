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
    private const int HotkeyId = 9001;
    private const int HotkeyJumpLastFolderId = 9002;
    private static readonly bool EnableExternalClipboardProviderForAltV = true;

    private readonly List<ClipboardEntry> _allItems = new();
    private readonly BulkObservableCollection<ClipboardEntry> _displayItems = new();
    private readonly ClipboardManager.Services.ClipboardSourceTracker _sourceTracker = new();

    /// <summary>FIFO/LIFO 下：多选 Enter 入队、新复制可自动入队；出队后条目不占批量角标，回到底部列表排序。</summary>
    private readonly BatchPasteQueueController _batchQueue = new();
#if CLIPX_CLIPBOARD
    private AltVClipboardProvider.Session? _batchQueueProviderSession;
    /// <summary>全局 Ctrl+V / Shift+Insert 松键出队防抖（毫秒，TickCount64）。</summary>
    private long _lastGlobalPasteQueueAdvanceTick;
    /// <summary>FIFO/LIFO 下列队已贴完，等待下一次他处粘键后切回普通模式（<see cref="AppSettings.BatchQueueAutoSwitchToNormalAfterQueueDone"/>）。</summary>
    private bool _batchQueueAwaitingNextPasteToSwitchOff;
    /// <summary>序列化「队列队首 → 剪贴板」写入，避免与监控钩交错或多路 TryPush 争用 OpenClipboard。</summary>
    private readonly SemaphoreSlim _queueClipboardPushLock = new(1, 1);
#endif
    private readonly SelectionCursorController _selectionCursor = new();
    private ClipboardEntry? _keyboardPointFocusEntry;
    private readonly List<(Border Row, Action Activate)> _batchMenuNav = new();
    private int _batchNavIndex;

    private IntPtr _hwnd;
    private IntPtr _targetWindow;
    private IntPtr _keyboardHook;
    private IntPtr _mouseHook;
    private IntPtr _winEventHook;
    private IntPtr _winEventHookFocus;

    /// <summary>
    /// WH_KEYBOARD_LL / WH_MOUSE_LL 回调由 user32 保存函数指针；Unhook 后仍可能再触发一两次。
    /// 若此时实例字段上的委托已被置 null，CLR 可能已回收闭环委托，导致「callback on garbage collected delegate」崩溃。
    /// 使用进程内长期存活的静态委托 + 当前拥有者指针，避免 lpfn 被回收。
    /// </summary>
    private static readonly Win32.LowLevelKeyboardProc s_popupKeyboardHookThunk = StaticKeyboardHookProc;
    private static readonly Win32.LowLevelMouseProc s_popupMouseHookThunk = StaticMouseHookProc;
    private static IntPtr s_popupKeyboardHookForNext;
    private static PopupWindow? s_popupKeyboardHookOwner;
    private static IntPtr s_popupMouseHookForNext;
    private static PopupWindow? s_popupMouseHookOwner;

    /// <summary>「点击对话框自动跳转首条路径」专用鼠标钩，与剪贴板弹窗钩分离以便弹窗关闭后仍可监听。</summary>
    private static readonly Win32.LowLevelMouseProc s_fileJumpAutoMouseThunk = StaticFileJumpAutoMouseProc;
    private static IntPtr s_fileJumpAutoMouseHookForNext;
    private static PopupWindow? s_fileJumpAutoMouseOwner;

#if CLIPX_FILEJUMP
    /// <summary>在系统「确定/保存/打开」等主按钮上点击时记录当前文件夹；与弹窗、首点自动跳转钩独立。</summary>
    private static readonly Win32.LowLevelMouseProc s_fileJumpPersistMouseThunk = StaticFileJumpPersistMouseProc;
    private static IntPtr s_fileJumpPersistMouseHookForNext;
    private static PopupWindow? s_fileJumpPersistMouseOwner;
#endif

    /// <summary>SetWinEventHook 同样会把托管委托交给系统；须用静态委托避免 Unhook 后晚到回调撞上已回收的实例委托。</summary>
    private static readonly Win32.WinEventDelegate s_popupWinEventThunk = StaticWinEventProc;
    private static PopupWindow? s_popupWinEventOwner;

    private bool _isSettingClipboard;
    /// <summary>最近一次自写完成时的剪贴板序列号；OnClipboardUpdate 看到序列号 ≤ 此值则视为自写回波。
    /// 解决 _isSettingClipboard 推迟清旗仍偶发漏标的根因（WPF SystemIdle 与 Win32 消息泵相对顺序并不严格保证）。</summary>
    private uint _lastSelfWriteClipboardSeq;
    /// <summary>最近一次自写的时间戳（TickCount64 ms），与 <see cref="_lastSelfWriteClipboardSeq"/> 配合做兜底窗口。</summary>
    private long _lastSelfWriteTickMs;
    /// <summary>自写回波时间窗（ms）：序列号未变 + 时间窗内即视为自写。</summary>
    private const int SelfWriteEchoWindowMs = 500;
    /// <summary>从历史粘贴整段流程中：禁止监控线程读剪贴板，避免 Contains/Get 与即将执行的 Set 在同 UI 线程上交错 OpenClipboard。</summary>
    private bool _pasteInProgress;
    /// <summary>连续粘贴多段（批量/队列）时保持 true，整轮结束后才清除 <see cref="_pasteInProgress"/>，避免段间剪贴板回波或 FIFO 自动入队插队。</summary>
    private bool _sequentialPasteHold;
    private bool _isPopupVisible;
    private readonly SearchEditorState _searchEditor = new();
    private string _searchText => _searchEditor.Text;
    private int _searchCaretIndex => _searchEditor.CaretIndex;
    private int _searchSelectionAnchor => _searchEditor.SelectionAnchor;
    private int _searchRefreshGeneration;
    private CancellationTokenSource? _searchRefreshCancellation;
    private bool _searchColdArchives = true;
    private bool _hasAppliedFilterResults;
    private string _lastAppliedFilterQuery = "";
    private EntryType? _lastAppliedFilterType;
    private bool _lastAppliedQuickPhraseOnly;
    private bool _lastAppliedSearchColdArchives;

    /// <summary>列表内 <see cref="SearchHighlightTextBlock"/> 绑定用：当前搜索词（Trim），随 <see cref="UpdateSearchUI"/> 更新。</summary>
    public static readonly DependencyProperty HighlightSearchQueryProperty = DependencyProperty.Register(
        nameof(HighlightSearchQuery),
        typeof(string),
        typeof(PopupWindow),
        new PropertyMetadata(""));

    public string HighlightSearchQuery
    {
        get => (string)GetValue(HighlightSearchQueryProperty);
        set => SetValue(HighlightSearchQueryProperty, value ?? "");
    }

    private EntryType? _typeFilter;
    private bool _quickPhraseOnly;
    private ClipboardEntry? _contextEntry;
    /// <summary>已按下 Alt，等待 KeyUp：无组合键则打开右键菜单。</summary>
    private bool _ctxAltAwaitRelease;
    private bool _ctxAltComboDuringRelease;
    /// <summary>右键菜单已打开时再次按下 Alt，松开时若无组合键则关闭菜单。</summary>
    private bool _ctxAltCloseMenuArmed;
    /// <summary>
    /// 含 Alt 的全局热键（如 Alt+`）打开面板后焦点仍在宿主（VS Code）；若用户先松 Alt，KeyUp 会进入宿主并抢走菜单焦点。
    /// 在短时内在本钩子中吞掉「热键收尾」的 Alt 松开；此期间不 arms <see cref="_ctxAltAwaitRelease"/>，避免与自动重复 Down 冲突。
    /// </summary>
    private bool _awaitHotkeyAltChordCleanup;
    private long _hotkeyAltChordCleanupDeadlineTick;
    /// <summary>与 RegisterHotKey 的 WM_HOTKEY 并行时防抖，避免 CycleBatchPasteMode 连跳两档。</summary>
    private long _cycleBatchPasteDebounceTick;
    /// <summary>与 WM_HOTKEY 并行时防抖，避免 TogglePopup 连切两次。</summary>
    private long _togglePopupDebounceTick;
    /// <summary>
    /// 主面板吞掉 Alt KeyDown 后，系统往往仍报告 Alt 未按下；锁存到 Alt KeyUp，供 Alt+/、Alt+` 等与 VkToChar 防录入对齐。
    /// </summary>
    private bool _swallowedMenuAltLatch;
    /// <summary>主面板吞掉右 Alt KeyDown 后锁存到 KeyUp，用于区分 RAlt+Enter 与原有 Alt+Enter。</summary>
    private bool _swallowedRightAltLatch;
    /// <summary>Win+V 被本程序拦截后，吞掉后续 Win KeyUp 以防止开始菜单弹出。</summary>
    private bool _winVIntercepted;
    private readonly List<(Border Row, Action Activate)> _contextMenuNav = new();
    private int _contextNavIndex;
    /// <summary>当前标为「待二次 Del 删除」的条目，与 <see cref="ClipboardEntry.IsPendingDelete"/> 同步。</summary>
    private ClipboardEntry? _pendingDeleteEntry;

    private int _pageSize = 8;
    private uint _panelPageScrollUpModifiers = Win32.MOD_CONTROL;
    private uint _panelPageScrollUpKey = 0xBD;
    private uint _panelPageScrollDownModifiers = Win32.MOD_CONTROL;
    private uint _panelPageScrollDownKey = 0xBB;
    private uint _starToggleHotkeyModifiers = Win32.MOD_CONTROL;
    private uint _starToggleHotkeyKey = 0x44;
    private int _firstVisibleIndex;

    private uint _hotkeyModifiers;
    private uint _hotkeyKey;
    private int _maxItems;
    private string _popupPosition = "Caret";
    private double _popupOpacity = 1.0;
    private bool _hideOnSameAppClick = true;
    private bool _popupPinned;
    private uint _passthroughModifierLatch;
    private string _panelModifierKey = "Ctrl";
    private bool _isDragging;
    private bool _userHasResized;
    private bool _isResizing;
    private Win32.POINT _dragLastPt;
    private long _lastDragMoveTick;
    private int _pendingDragX, _pendingDragY;
    private bool _hasPendingDragMove;
    /// <summary>标题栏拖动时由鼠标钩 SetWindowPos 维护的 HWND 物理左上角；用于识别 Shell/贴靠对窗口的偷跑。</summary>
    private int _hookAuthPhysLeft, _hookAuthPhysTop;
    /// <summary>标题栏拖动松手后第一次 Sync：若 HWND 已被壳/DWM 甩离钩子最后一帧位置则拉回（与 H15 日志配对）。</summary>
    private int _postDragHookAuthLeft = int.MinValue, _postDragHookAuthTop;
    /// <summary>WM_DPICHANGED 后若干次 WINDOWPOSCHANGING 不再强制 SWP_NOMOVE，否则系统无法应用 DPI 建议矩形。</summary>
    private int _windowPosNomoveSkipCount;
    private bool _clickReceivedByPopup;
    private int _pendingPhysX, _pendingPhysY;
    private bool _isOurSetWindowPos;
    /// <summary>上次成功通过 caret/UIA 解析到的目标窗口对应的 caret 物理像素位置（含 caretGap）；用于自绘 caret 应用（Word/Office/Chromium）冷启动失败时的兜底。</summary>
    private IntPtr _lastCaretCacheHwnd = IntPtr.Zero;
    private int _lastCaretCachePhysX, _lastCaretCachePhysY;
    private long _lastCaretCacheTickMs;
    /// <summary>Show/UpdateLayout 过程中阻止 WPF 改写位置，避免先出现在 (0,0) 或顶边再跳到目标点。</summary>
    private bool _lockPopupWindowNomove;
    private List<QuickPasteEntry> _quickPastes = new();
    private readonly ClipboardHistoryStore _historyStore = new();
    private ImageOcrQueue? _imageOcrQueue;
    private AppSettings? _appSettings;
    private IntPtr _lastForegroundForDialogTrack = IntPtr.Zero;
    private long _lastFileDialogSeenTick;
    private const int FileDialogAliveWindowMs = 2000;
    /// <summary>前台切换风暴时合并到单次 UI 回调，避免关闭跳转列表时 Dispatcher 队列爆炸。</summary>
    private int _foregroundChangeCoalesceGen;
    /// <summary>自上次 UI 处理以来，原生 WinEvent 前台回调次数（合并前）。</summary>
    private int _foregroundNativeBurst;
    /// <summary>因序号过期而跳过的 UI 调度次数（合并丢弃的 BeginInvoke）。</summary>
    private int _foregroundUiDispatchSuperseded;

    #region agent log
    private int _agentDbgDragMoveLogCount;
    private int _agentDbgH17WpcSkipLogCount;
    private int _agentDbgH20LogCount;
    private int _agentDbgCachedPrimarySeamX = int.MinValue;
    private int _agentDbgH21MismatchLogCount;

    /// <summary>历史调试入口；统一转到异步诊断日志，避免同步写项目根目录造成 UI 抖动。</summary>
    private static void AgentDbgLog(string hypothesisId, string location, string message, object? data = null)
    {
        try
        {
            var payload = JsonSerializer.Serialize(new
            {
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                hypothesisId,
                location,
                message,
                data
            });
            ClipboardDiagnosticsLog.Write($"agent {payload}");
        }
        catch { /* 调试日志失败不影响主流程 */ }
    }
    #endregion

    /// <summary>文件对话框跳转：2 秒内第二次 Ctrl+G 直接跳默认项（与列表预选一致）。</summary>
    private const int FileJumpDoubleTapMs = 2000;

    private long _fileJumpLastHotkeyTick;
    private IntPtr _fileJumpLastDialogHwnd = IntPtr.Zero;
    private FileDialogJumpPickerWindow? _activeFileJumpPicker;
    /// <summary>
    /// <see cref="FileDialogJumpPickerWindow"/> 的 ShowDialog 会嵌套消息循环，同优先级的 BeginInvoke 仍可运行；
    /// 若没有此项，狂按跳转热键时会再次进入 ShowDialog，嵌套模态窗导致状态错乱甚至进程退出。
    /// </summary>
    private bool _fileJumpPickerOpenInProgress;
    private int _fileJumpPickerSession;
    private DispatcherTimer? _fileJumpOpenDelayTimer;
    private DispatcherTimer? _fileJumpAutoOpenDebounceTimer;
    private IntPtr _fileJumpAutoOpenRetryRoot;
    private int _fileJumpAutoOpenRetryCount;
    private int _fileJumpDelaySession;
    /// <summary>「对话框到前台自动执行」路径采集异步化，避免与 UI 线程争抢；递增后过时结果丢弃。</summary>
    private int _fileJumpAutoForegroundCollectGen;
    /// <summary>手动跳转热键路径采集异步化；连按热键时仅最后一次结果生效（极端情况下或影响「双按直跳」窗口期）。</summary>
    private int _fileJumpHotkeyCollectGen;
    private uint _fileJumpHotkeyModifiers;
    private uint _fileJumpHotkeyKey;

    private IntPtr _fileJumpAutoMouseHook;
#if CLIPX_FILEJUMP
    private IntPtr _fileJumpPersistMouseHook;
#endif
    /// <summary>待监听左键的文件对话框 HWND（与前台识别一致）。</summary>
    private IntPtr _fileJumpAutoArmedDialog;
    /// <summary>同上对话框的顶层 HWND，用于判断点击是否落在该对话框 UI 内。</summary>
    private IntPtr _fileJumpAutoArmedRoot;

    /// <summary>已对其实施过「点击后自动跳转」的对话框顶层 HWND；同一窗口存续期间仅跳转一次，避免失焦再切回后重复跳转。</summary>
    private IntPtr _fileJumpAutoFirstJumpDoneRoot;

    /// <summary>已因「对话框成为前台」自动弹出过跳转列表的顶层 HWND；关掉对话框再开才会再次自动弹。</summary>
    private IntPtr _fileJumpAutoOpenPickerDoneRoot;

    /// <summary>「切回对话框自动同步路径」采集代数，递增后过时结果丢弃。</summary>
    private int _fileJumpAutoSyncCollectGen;
    /// <summary>分层等待调度代数：新一次前台切换后旧定时器结果丢弃。</summary>
    private int _fileJumpAutoSyncScheduleGen;
    /// <summary>对话框路径快照分层等待调度代数。</summary>
    private int _dialogSnapshotScheduleGen;
    private IntPtr _snapshotFolderDebounceHwnd;
    private long _snapshotFolderDebounceTick;
    /// <summary>最近一次离开外部文件管理器时记录到的路径；用于切回文件对话框时优先同步。</summary>
    private string _lastExternalFolder = "";
    private IntPtr _lastExternalManagerRoot = IntPtr.Zero;
    /// <summary>
    /// 前台切换只投递最新一组窗口，实际 Shell COM / UIA 路径解析由单个后台 STA worker 串行完成。
    /// 不能在 WPF Dispatcher 上直接解析：Shell 缓存过期或 Explorer 忙时一次调用可阻塞数秒。
    /// </summary>
    private readonly object _externalFolderResolveGate = new();
    private IntPtr _pendingDialogFolderResolvePrevious;
    private IntPtr _pendingExternalFolderResolvePrevious;
    private IntPtr _pendingExternalFolderResolveCurrent;
    private bool _externalFolderResolveWorkerRunning;
    private volatile bool _externalFolderResolveStopping = false;
    /// <summary>Picker 打开时轮询 Explorer 窗口路径变化的定时器。</summary>
    private System.Windows.Threading.DispatcherTimer? _explorerPathPollTimer;
    private string _explorerPathPollLastPath = "";
    private IntPtr _explorerPathPollHwnd;
    private IntPtr _fileJumpNavigationSuppressRoot;
    private string _fileJumpNavigationSuppressPath = "";
    private long _fileJumpNavigationSuppressUntilTick;

    public event Action? SettingsRequested;

    /// <summary>
    /// 呼出剪贴板面板时前台为开始菜单/搜索等 Shell；Win11 等系统可能将此类界面置于普通应用 HWND_TOPMOST 之上，
    /// 用户态无法可靠置顶，订阅方可提示用户先按 Esc 关闭 Shell。
    /// </summary>
    public event Action? ShellForegroundMayOccludePopup;

    /// <summary>批量模式（普通 / LIFO / FIFO）切换后通知，用于托盘图标等。</summary>
    public event EventHandler? BatchPasteModeChanged;

    private volatile bool _isPhraseEditPopupOpen;
    private volatile bool _isTextEntryEditPopupOpen;
    private volatile bool _isBatchMenuPopupOpen;
    private volatile bool _isContextPopupOpen;

    private int _previewImageFileIndex;
    private volatile string[]? _previewImageFiles;
    private ClipboardEntry? _previewImageFilesSource;
}
