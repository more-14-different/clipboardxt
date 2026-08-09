using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WinForms = System.Windows.Forms;
using Application = System.Windows.Application;

namespace ClipboardManager;

public partial class App : Application
{
    private static Mutex? _mutex;
    private WinForms.NotifyIcon? _trayIcon;
    private long _lastShellForegroundOcclusionBalloonTick;
    private PopupWindow? _popup;
#if CLIPX_CLIPBOARD
    private BatchModeCycleHotkeyHost? _batchModeHotkeyHost;
#endif
#if CLIPX_FILEJUMP
    private ExplorerQuickFindController? _explorerQuickFind;
#endif
    private AppSettings _settings = new();
    private static bool _probingAssemblyResolveRegistered;

    /// <summary>
    /// 部分宿主下 <see cref="AppContext.BaseDirectory"/> 与主程序集所在目录不一致，会导致无法找到 NPinyin 等旁路 dll；
    /// 从 <see cref="Assembly.Location"/> 目录补解析（单文件时 Location 为空，回退 BaseDirectory）。
    /// </summary>
    private static void RegisterProbingAssemblyResolve()
    {
        if (_probingAssemblyResolveRegistered) return;
        _probingAssemblyResolveRegistered = true;

        AppDomain.CurrentDomain.AssemblyResolve += (_, args) =>
        {
            try
            {
                var an = new AssemblyName(args.Name);
                if (string.IsNullOrEmpty(an.Name)) return null;

                var dir = GetDependencyProbeDirectory();
                if (string.IsNullOrEmpty(dir)) return null;

                var dll = Path.Combine(dir, an.Name + ".dll");
                if (File.Exists(dll))
                    return AssemblyLoadContext.Default.LoadFromAssemblyPath(dll);

                if (!string.IsNullOrEmpty(an.CultureName))
                {
                    var sat = Path.Combine(dir, an.CultureName, an.Name + ".dll");
                    if (File.Exists(sat))
                        return AssemblyLoadContext.Default.LoadFromAssemblyPath(sat);
                }
            }
            catch
            {
                /* 由 CLR 继续尝试默认探测 */
            }
            return null;
        };
    }

    private static string? GetDependencyProbeDirectory()
    {
        try
        {
            var loc = typeof(App).Assembly.Location;
            if (!string.IsNullOrEmpty(loc))
            {
                var d = Path.GetDirectoryName(loc);
                if (!string.IsNullOrEmpty(d)) return d;
            }
        }
        catch
        {
            /* ignore */
        }

        var b = AppContext.BaseDirectory;
        if (string.IsNullOrEmpty(b)) return null;
        return b.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        RegisterProbingAssemblyResolve();
        base.OnStartup(e);

        AppPaths.Initialize(PerUserInstall.IsRunningFromInstallLocation());

        if (AltVClipboardProvider.TryHandleCommandLine(e.Args, out var providerExitCode))
        {
            Shutdown(providerExitCode);
            return;
        }

        // WinForms 托盘/上下文菜单在 WPF 宿主中更稳妥
        WinForms.Application.EnableVisualStyles();
        WinForms.Application.SetCompatibleTextRenderingDefault(false);

        if (PerUserInstall.TryProcessUninstallArgs(e.Args))
        {
            Shutdown();
            return;
        }

        _settings = AppSettings.Load();
        UiLanguage.Initialize(_settings.UiLanguage);

        if (_settings.RunAsAdministrator && !ProcessElevation.IsCurrentProcessElevated())
        {
            if (ProcessElevation.TryStartElevatedCopyAndExit(e.Args))
            {
                Shutdown(0);
                return;
            }
        }

        _mutex = new Mutex(true, AppPaths.MutexName, out bool isNew);
        if (!isNew)
        {
#if DEBUG
            try { Console.WriteLine("ClipboardX 已在运行中（互斥锁），本进程将退出。请查看托盘或结束旧进程。"); } catch { }
#endif
            LocalizedMessageBox.Show("ClipboardX 已在运行中", "提示",
                MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        ThemeManager.Apply(_settings.Theme);
        // 启动时如果启用了替换 Win+V，先禁用系统剪贴板历史
        if (_settings.ReplaceSystemWinV)
            SystemClipboardHelper.SetSystemClipboardHistoryEnabled(false);
        PerUserInstall.EnsureUninstallRegistrationIfNeeded();
        StartupRegistration.Apply(_settings.RunAtStartup, _settings.RunAsAdministrator);

        _popup = new PopupWindow();
        _popup.Initialize(_settings);
        _popup.SettingsRequested += OpenSettings;
        _popup.BatchPasteModeChanged += (_, _) =>
            Dispatcher.Invoke(RefreshTrayIcon);

#if CLIPX_CLIPBOARD
        EnsureBatchModeHotkeyHost();
        if (!_batchModeHotkeyHost!.TryRegister(_settings.BatchModeCycleHotkeyModifiers, _settings.BatchModeCycleHotkeyKey))
        {
            LocalizedMessageBox.Show(
                $"批量模式切换快捷键 {_settings.BatchModeCycleHotkeyDisplayName} 注册失败，可能与其他软件冲突",
                "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
#endif

        SetupTrayIcon(e.Args);
        _popup.ShellForegroundMayOccludePopup += OnPopupShellForegroundMayOcclude;
#if CLIPX_FILEJUMP
        SyncExplorerQuickFindHook();
#endif
        _ = CheckForUpdatesOnStartupAsync();
    }

    private void OnPopupShellForegroundMayOcclude()
    {
        if (_trayIcon == null) return;
        var now = Environment.TickCount64;
        if (now - _lastShellForegroundOcclusionBalloonTick < 120_000)
            return;
        _lastShellForegroundOcclusionBalloonTick = now;

        _trayIcon.ShowBalloonTip(
            10000,
            "ClipboardX",
            UiLanguage.T("开始菜单或搜索打开时，剪贴板窗口可能被系统界面挡住，属系统限制。请先按 Esc 关闭开始菜单或搜索，再按热键呼出。"),
            WinForms.ToolTipIcon.Info);
    }

    private static void ShowAboutDialog()
    {
        var body =
            $"版本：{AppInfo.DisplayVersion}\n" +
            $"GitHub：{AppInfo.GitHubUrl}\n\n" +
            "作者：mact\n" +
            "邮箱：chaoji000010@163.com";
        LocalizedMessageBox.Show(
            body,
            "关于 ClipboardX",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    /// <summary>与托盘相同图稿，用于 WPF 窗口标题栏。</summary>
    public static ImageSource GetWindowIconSource()
    {
        using var icon = TrayIconSvg.CreateIcon(32);
        return Imaging.CreateBitmapSourceFromHIcon(
            icon.Handle,
            System.Windows.Int32Rect.Empty,
            BitmapSizeOptions.FromEmptyOptions());
    }

    protected override void OnExit(ExitEventArgs e)
    {
        AppSettings.FlushPendingSave();

        if (_settings.ClearHistoryOnExit)
            _popup?.ClearHistory();

        if (_settings.ReplaceSystemWinV)
            SystemClipboardHelper.SetSystemClipboardHistoryEnabled(true);

#if CLIPX_CLIPBOARD
        _batchModeHotkeyHost?.DisposeHost();
        _batchModeHotkeyHost = null;
#endif
        _popup?.Cleanup();
#if CLIPX_FILEJUMP
        _explorerQuickFind?.Dispose();
        _explorerQuickFind = null;
#endif
        if (_trayIcon != null)
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
        }
        ShellNavigateLog.FlushPending();
        _mutex?.ReleaseMutex();
        _mutex?.Dispose();
        base.OnExit(e);
    }
}
