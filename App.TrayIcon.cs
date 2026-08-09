using System.IO;
using System.Windows;
using Drawing = System.Drawing;
using WinForms = System.Windows.Forms;
using Application = System.Windows.Application;

namespace ClipboardManager;

public partial class App : Application
{
    private void SetupTrayIcon(string[] startupArgs)
    {
        _trayIcon = new WinForms.NotifyIcon
        {
            Icon = CreateTrayIconFromSettings(),
            Text = $"ClipboardX ({_settings.HotkeyDisplayName})",
            Visible = true
        };

        var menu = new WinForms.ContextMenuStrip { ShowItemToolTips = true };
#if CLIPX_CLIPBOARD
        menu.Items.Add($"显示 ({_settings.HotkeyDisplayName})", null, (_, _) =>
            Dispatcher.BeginInvoke(() => _popup?.TogglePopup()));
#endif
        menu.Items.Add("设置", null, (_, _) =>
            Dispatcher.BeginInvoke(() => OpenSettings()));
#if CLIPX_CLIPBOARD
        var pinyinMenu = new WinForms.ToolStripMenuItem("中文过滤");
        var traditionalPinyinItem = new WinForms.ToolStripMenuItem("传统拼音")
        {
            CheckOnClick = false,
            ToolTipText = "使用全拼和首字母过滤中文，例如 nihao / nh。"
        };
        var xiaohePinyinItem = new WinForms.ToolStripMenuItem("小鹤双拼")
        {
            CheckOnClick = false,
            ToolTipText = "使用小鹤双拼全码和声母串过滤中文。"
        };

        void RefreshPinyinMenuChecks()
        {
            var mode = PinyinFilterModes.Normalize(_settings.PinyinFilterMode);
            traditionalPinyinItem.Checked = mode == PinyinFilterModes.Traditional;
            xiaohePinyinItem.Checked = mode == PinyinFilterModes.Xiaohe;
        }

        traditionalPinyinItem.Click += (_, _) =>
        {
            Dispatcher.BeginInvoke(() =>
            {
                SetPinyinFilterMode(PinyinFilterModes.Traditional);
                RefreshPinyinMenuChecks();
            });
        };
        xiaohePinyinItem.Click += (_, _) =>
        {
            Dispatcher.BeginInvoke(() =>
            {
                SetPinyinFilterMode(PinyinFilterModes.Xiaohe);
                RefreshPinyinMenuChecks();
            });
        };
        RefreshPinyinMenuChecks();
        pinyinMenu.DropDownItems.Add(traditionalPinyinItem);
        pinyinMenu.DropDownItems.Add(xiaohePinyinItem);
        menu.Items.Add(pinyinMenu);
#endif
        menu.Items.Add("关于", null, (_, _) =>
            Dispatcher.BeginInvoke(() => ShowAboutDialog()));
        menu.Items.Add("检查更新…", null, (_, _) =>
            _ = CheckForUpdatesAsync());
#if DEBUG
        menu.Items.Add("采集窗口信息", null, (_, _) => StartWindowInspection());
#endif
#if CLIPX_FILEJUMP
        menu.Items.Add("添加自定义文件对话框…", null, (_, _) => StartCustomFileDialogWizard());
#endif
        menu.Items.Add(new WinForms.ToolStripSeparator());
        if (PerUserInstall.IsRunningFromInstallLocation())
        {
            menu.Items.Add("卸载…", null, (_, _) =>
                Dispatcher.BeginInvoke(() => PerUserInstall.PromptUninstallFromTray()));
        }
        else if (File.Exists(PerUserInstall.InstalledExecutablePath))
        {
            menu.Items.Add(new WinForms.ToolStripMenuItem("安装到当前用户…")
            {
                Enabled = false,
                ToolTipText =
                    "已在用户「程序」目录安装 ClipboardX。请从开始菜单或安装位置启动本程序后使用托盘「卸载」，或先卸载后再从此副本安装。"
            });
        }
        else
        {
            menu.Items.Add("安装到当前用户…", null, (_, _) =>
            {
#if DEBUG
                LocalizedMessageBox.Show(
                    "当前为调试构建，不会复制到「程序」安装目录。请使用 Release 产物测试安装菜单。",
                    PerUserInstall.DisplayName,
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
#else
                if (PerUserInstall.TryInstallToUserProgramsAndRelaunch(startupArgs))
                    Dispatcher.BeginInvoke(() => Shutdown());
#endif
            });
        }

        menu.Items.Add("退出", null, (_, _) =>
            Dispatcher.BeginInvoke(() => Shutdown()));
        _trayIcon.ContextMenuStrip = menu;
        UiLanguage.ApplyTo(menu);
        UiLanguage.Changed += RefreshTrayLanguage;

#if CLIPX_CLIPBOARD
        _trayIcon.DoubleClick += (_, _) =>
            Dispatcher.BeginInvoke(() => _popup?.TogglePopup());
#endif

#if DEBUG
        try
        {
            Console.WriteLine("ClipboardX 已在运行。");
            Console.WriteLine("- dotnet run 时任务管理器里进程名多为「dotnet」，属于正常情况。");
            Console.WriteLine("- 图标在系统托盘：任务栏右侧「显示隐藏的图标」展开查找（蓝色剪贴板样式）。");
            Console.WriteLine("- 若已有一份在跑，再运行会弹窗提示「已在运行中」。");
            Console.WriteLine("按 Ctrl+C 可结束本控制台（会结束当前这次调试进程）。");
        }
        catch { /* 无控制台时忽略 */ }
#endif
    }

    private void RefreshTrayLanguage() => UiLanguage.ApplyTo(_trayIcon?.ContextMenuStrip);

    private void UpdateTrayTooltip()
    {
        if (_trayIcon != null)
            _trayIcon.Text = $"ClipboardX ({_settings.HotkeyDisplayName})";
    }

    private void RefreshTrayIcon()
    {
        if (_trayIcon == null) return;
        try
        {
            var old = _trayIcon.Icon;
            _trayIcon.Icon = CreateTrayIconFromSettings();
            old?.Dispose();
        }
        catch
        {
            /* 换图标失败时保留旧 Icon */
        }
    }

    private Drawing.Icon CreateTrayIconFromSettings()
    {
        var mode = Enum.TryParse<BatchPasteQueueMode>(_settings.BatchPasteMode, true, out var m)
            ? m
            : BatchPasteQueueMode.Off;
        return TrayIconSvg.CreateIcon(32, mode);
    }
}
