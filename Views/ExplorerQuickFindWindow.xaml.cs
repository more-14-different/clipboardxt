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

/// <summary>
/// 资源管理器上下文中 Everything 筛选结果浮层（不抢焦点；键盘由低级钩下发）。
/// 视觉与「文件对话框跳转」浮层共用 SharedPopupStyles。
/// 窗口采用 Hide/Show 复用，避免每次会话重建开销。
/// </summary>
public partial class ExplorerQuickFindWindow : Window
{
    private Brush? _primaryBrush;
    private Brush? _secondaryBrush;
    private Brush? _mutedBrush;
    private Brush? _highlightBrush;
    private IntPtr _hwnd;
    private bool _userHasResized;
    private AppSettings? _settings;

    private const string DefaultHint = "↑↓ 选择 · ←→ 翻页 · Ctrl+N 快选 · Enter 定位 · Esc 关闭";

    public ExplorerQuickFindWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => CacheBrushes();
        SourceInitialized += (_, _) =>
        {
            var helper = new WindowInteropHelper(this);
            helper.EnsureHandle();
            _hwnd = helper.Handle;
            HwndSource.FromHwnd(_hwnd)?.AddHook(WndProc);
        };
        Closed += (_, _) =>
        {
            SaveSize();
            UserClosed?.Invoke(this, EventArgs.Empty);
        };
    }

    public event EventHandler? UserClosed;

    /// <summary>用户在列表中点击了某项，携带完整路径。</summary>
    public event Action<string>? ItemActivated;
}
