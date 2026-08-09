using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using ClipboardX.Mac.Views;

namespace ClipboardX.Mac;

internal sealed class MacTrayIcon : IDisposable
{
    private readonly TrayIcon _tray;

    private MacTrayIcon(TrayIcon tray) => _tray = tray;

    public static MacTrayIcon Create(MainWindow main, MacSettings settings)
    {
        var tray = new TrayIcon
        {
            ToolTipText = "ClipboardX",
            IsVisible = true,
            Icon = null
        };
        void RebuildMenu() => tray.Menu = BuildMenu(main, settings, () =>
        {
            settings.UiLanguage = MacUiText.IsEnglish(settings) ? "zh-CN" : "en-US";
            settings.Save();
            main.ApplyLanguage();
            RebuildMenu();
        });
        RebuildMenu();
        tray.Clicked += (_, _) => Avalonia.Threading.Dispatcher.UIThread.Post(main.ToggleVisible);
        return new MacTrayIcon(tray);
    }

    private static NativeMenu BuildMenu(MainWindow main, MacSettings settings, Action toggleLanguage)
    {
        var english = MacUiText.IsEnglish(settings);
        var show = new NativeMenuItem(english ? "Show Clipboard Panel" : "显示剪贴板面板");
        show.Click += (_, _) => Avalonia.Threading.Dispatcher.UIThread.Post(main.ShowFromTray);

        var language = new NativeMenuItem(english ? "Interface Language: English" : "界面语言: 简体中文");
        language.Click += (_, _) => Avalonia.Threading.Dispatcher.UIThread.Post(toggleLanguage);

        var quit = new NativeMenuItem(english ? "Exit" : "退出");
        quit.Click += (_, _) =>
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime d)
                d.Shutdown();
        };

        var menu = new NativeMenu();
        menu.Items.Add(show);
        menu.Items.Add(language);
        menu.Items.Add(new NativeMenuItemSeparator());
        menu.Items.Add(quit);
        return menu;
    }

    public void Dispose() => _tray.Dispose();
}
