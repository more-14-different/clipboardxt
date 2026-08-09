using ClipboardManager.Models;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace ClipboardManager;

public partial class FileDialogJumpPickerWindow
{
    private const int PageSize = 8;

    private static readonly Win32.LowLevelKeyboardProc s_jumpPickerKbThunk = StaticJumpPickerKeyboardHook;
    private static readonly Win32.LowLevelMouseProc s_jumpPickerMouseThunk = StaticJumpPickerMouseHook;
    private static readonly Win32.WinEventDelegate s_jumpPickerWinEventThunk = StaticJumpPickerWinEventProc;
    private static readonly Win32.WinEventDelegate s_jumpPickerDockWinEventThunk = StaticJumpPickerDockWinEventProc;
    private static readonly Win32.WinEventDelegate s_jumpPickerOwnerDestroyThunk = StaticJumpPickerOwnerDestroyProc;
    private static IntPtr s_jumpPickerKbHookForNext;
    private static FileDialogJumpPickerWindow? s_jumpPickerKbOwner;
    private static IntPtr s_jumpPickerMouseHookForNext;
    private static FileDialogJumpPickerWindow? s_jumpPickerMouseOwner;
    private static FileDialogJumpPickerWindow? s_jumpPickerWinEventOwner;
    private static FileDialogJumpPickerWindow? s_jumpPickerDockWinEventOwner;
    private static FileDialogJumpPickerWindow? s_jumpPickerOwnerDestroyOwner;

    private readonly IntPtr _fileDialogOwnerHwnd;
    private readonly int _mouseScreenX;
    private readonly int _mouseScreenY;
    private readonly AppSettings _settings;
    private readonly bool _dockBesideDialog;
    private readonly bool _autoForegroundStickyMode;
    private readonly List<FileJumpCandidate> _collectorSnapshot;
    private readonly bool _isStandaloneMode;
    private readonly IntPtr _standaloneTargetHwnd;
    private readonly Func<string, IntPtr, Task>? _standalonePasteCallback;
    private readonly IntPtr _ownerFocusHwndBeforePickerActivation;

    private IntPtr _jumpKeyboardHook;
    private bool _restoreOwnerFocusOnClose;
    private bool _suppressJumpHook;
    private volatile bool _suppressJumpHookForClipboardPopup;
    private IntPtr _hwnd;
    private bool _isOurSetWindowPosForPicker;
    private bool _lockJumpPickerNomove;
    private IntPtr _jumpPickerMouseHook;
    private bool _clickReceivedByJumpPicker;
    private bool _suppressDismissForSubDialog;
    private volatile bool _suppressDismissForClipboardPopup;
    private volatile bool _isPickerReadyForMouseHook;
    private IntPtr _jumpPickerWinEventHook;
    private IntPtr _ownerDestroyHook;

    private DispatcherTimer? _dockFollowTimer;
    private IntPtr _dockOwnerMoveSizeHook;
    private IntPtr _dockOwnerLocationHook;
    private bool _dockOwnerMoveActive;
    private List<FileJumpCandidate>? _deferredExternalRefresh;
    private string? _deferredExternalPreferredPath;
    private DispatcherTimer? _deferredExternalRefreshTimer;
    private int _dockPopupPhysWidth;
    private int _dockPopupPhysHeight;
    private int _lastDockOwnerLeft = int.MinValue;
    private int _lastDockOwnerTop = int.MinValue;
    private int _lastDockOwnerRight = int.MinValue;
    private int _lastDockOwnerBottom = int.MinValue;
    private int _lastDockActualWidth = int.MinValue;
    private int _lastDockActualHeight = int.MinValue;
    private int _lastAppliedPhysX = int.MinValue;
    private int _lastAppliedPhysY = int.MinValue;
    private DispatcherTimer? _focusRetryTimer;
    private DispatcherTimer? _searchRefreshTimer;
    private int _focusRetryCount;
    private int _perfDockFollowSlowLogCount;
    private int _perfFocusSlowLogCount;
    private int _pendingPhysX;
    private int _pendingPhysY;
    private bool _snappedPhysicalOnce;

    private readonly SearchEditorState _searchEditor = new();
    private string _searchText => _searchEditor.Text;
    private int _searchCaretIndex => _searchEditor.CaretIndex;
    private int _searchSelectionAnchor => _searchEditor.SelectionAnchor;
    private volatile bool _hasSearchText;
    private bool _userHasResized;
    private bool _isResizing;
    private long _loadedTick;

    public static readonly DependencyProperty HighlightSearchQueryProperty = DependencyProperty.Register(
        nameof(HighlightSearchQuery),
        typeof(string),
        typeof(FileDialogJumpPickerWindow),
        new PropertyMetadata(""));

    public string HighlightSearchQuery
    {
        get => (string)GetValue(HighlightSearchQueryProperty);
        set => SetValue(HighlightSearchQueryProperty, value ?? "");
    }

    private FileJumpPickerFilterMode _filterMode = FileJumpPickerFilterMode.All;
    private int _firstVisibleIndex;
    private int _commitNavigateKeepOpenGen;
    private string _commitNavigateKeepOpenPath = "";
    private long _commitNavigateKeepOpenUntilTick;
    private readonly List<FileJumpPickerRow> _masterRows = [];
    private readonly BulkObservableCollection<FileJumpPickerRow> _displayRows = [];
    private readonly List<string> _everythingFolderPaths = [];
    private string _everythingPathsValidForQuery = "";
    private int _everythingQueryGen;
    private CancellationTokenSource? _everythingQueryCts;

    public string? SelectedPath { get; private set; }
    public IntPtr OwnerDialogHwnd => _fileDialogOwnerHwnd;
    public bool IsAutoForegroundStickyMode => _autoForegroundStickyMode;
    internal bool CanReceiveSearchPaste => IsLoaded && IsVisible;

    internal void SetClipboardPopupInteraction(bool active)
    {
        _suppressJumpHookForClipboardPopup = active;
        _suppressDismissForClipboardPopup = active;
    }

    private bool IsDismissSuppressed =>
        _suppressDismissForSubDialog || _suppressDismissForClipboardPopup;

    private static void PerfLog(string eventName, long elapsedMs, long thresholdMs, string detail = "")
    {
        if (elapsedMs < thresholdMs) return;
        ClipboardDiagnosticsLog.Write(
            string.IsNullOrEmpty(detail)
                ? $"filejump.perf {eventName} elapsedMs={elapsedMs}"
                : $"filejump.perf {eventName} elapsedMs={elapsedMs} {detail}");
    }
}
