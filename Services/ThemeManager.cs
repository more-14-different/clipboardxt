using System.Windows.Media;
using Application = System.Windows.Application;
using Color = System.Windows.Media.Color;

namespace ClipboardManager;

public static class ThemeManager
{
    /// <summary>与托盘/「普通」模式 Tag 同源（<see cref="TrayIconSvg.MainBlueHex"/>），列表选中与强调色统一其上。</summary>
    private const byte BrandR = 0x13, BrandG = 0x94, BrandB = 0x93;

    private const byte DarkEditorBg = 0x1E;

    /// <summary>暗色编辑器底上叠品牌青绿，与 <see cref="AccentBg"/> 色相一致。</summary>
    private static SolidColorBrush MixBrandOnDarkBg(int brandNumerator, int bgNumerator) =>
        B(
            MixCh(BrandR, DarkEditorBg, brandNumerator, bgNumerator),
            MixCh(BrandG, DarkEditorBg, brandNumerator, bgNumerator),
            MixCh(BrandB, DarkEditorBg, brandNumerator, bgNumerator));

    private static byte MixCh(byte fg, byte bg, int wFg, int wBg)
    {
        var d = wFg + wBg;
        return d <= 0 ? bg : (byte)((fg * (long)wFg + bg * (long)wBg) / d);
    }

    public static void Apply(string theme)
    {
        bool dark = theme switch
        {
            "Dark" => true,
            "Light" => false,
            _ => IsSystemDark()
        };
        ApplyColors(dark);
    }

    private static void ApplyColors(bool dark)
    {
        var r = Application.Current.Resources;
        if (dark)
        {
            // 底栏布局仍偏 VS Code；选中/强调与品牌青绿 #139493 统一
            r["PopupBgBrush"] = B(0xF2, 0x1E, 0x1E, 0x1E);
            r["WindowBgBrush"] = B(0x1E, 0x1E, 0x1E);
            r["SurfaceBrush"] = B(0x25, 0x25, 0x26);
            // 列表悬停 / 选中：与 AccentBg 同系混合，避免与 VS Code 蓝或旧灰绿脱节
            r["HoverBrush"] = MixBrandOnDarkBg(7, 18);
            r["SelectedBrush"] = MixBrandOnDarkBg(12, 13);
            r["FooterBrush"] = B(0x25, 0x25, 0x26);
            r["PrimaryText"] = B(0xCC, 0xCC, 0xCC);
            r["SecondaryText"] = B(0x9D, 0x9D, 0x9D);
            r["MutedText"] = B(0x85, 0x85, 0x85);
            r["HintText"] = B(0xA0, 0xA0, 0xA0);
            r["PreviewAppText"] = B(0x8F, 0xA9, 0xB7);
            r["AccentBg"] = B(BrandR, BrandG, BrandB);
            r["AccentFg"] = B(0xFF, 0xFF, 0xFF);
            // 面板说明：低饱和莫兰迪色，按操作语义复用，不参与品牌青绿体系。
            r["HintActionBrush"] = B(0x9E, 0xAB, 0xC3);     // 灰蓝：执行
            r["HintTransferBrush"] = B(0xB6, 0xA0, 0xB8);   // 灰紫：传递
            r["HintSearchBrush"] = B(0xC2, 0xAD, 0x8F);     // 暖沙：搜索
            r["HintNavigationBrush"] = B(0x8F, 0xA9, 0xB7); // 雾蓝：导航
            r["HintFilterBrush"] = B(0xA8, 0xA0, 0xC0);     // 薰衣草灰：筛选
            r["HintManageBrush"] = B(0xC1, 0x9A, 0xA6);     // 干枯玫瑰：管理
            r["HintExitBrush"] = B(0xC4, 0x9B, 0x8B);       // 陶土：退出
            r["ThemeBorder"] = B(0x3E, 0x3E, 0x42);
            r["ThemeSeparator"] = B(0x3E, 0x3E, 0x42);
            r["DangerBg"] = B(0xF4, 0x87, 0x71);
            r["ScrollBarTrackBrush"] = B(0x1E, 0x1E, 0x1E);
            r["ScrollBarThumbBrush"] = B(0x42, 0x42, 0x42);
            r["ScrollBarThumbHoverBrush"] = B(0x4F, 0x4F, 0x4F);
        }
        else
        {
            r["PopupBgBrush"] = B(0xF5, 0xEF, 0xF1, 0xF5);
            r["WindowBgBrush"] = B(0xEF, 0xF1, 0xF5);
            r["SurfaceBrush"] = B(0xE6, 0xE9, 0xEF);
            r["HoverBrush"] = B(0xE0, 0xF0, 0xEE);
            r["SelectedBrush"] = B(0xCE, 0xE8, 0xE6);
            r["FooterBrush"] = B(0xCC, 0xD0, 0xDA);
            r["PrimaryText"] = B(0x4C, 0x4F, 0x69);
            r["SecondaryText"] = B(0x8C, 0x8F, 0xA1);
            r["MutedText"] = B(0x9C, 0xA0, 0xB0);
            r["HintText"] = B(0x7C, 0x7F, 0x93);
            r["PreviewAppText"] = B(0x5F, 0x78, 0x86);
            r["AccentBg"] = B(BrandR, BrandG, BrandB);
            r["AccentFg"] = B(0xFF, 0xFF, 0xFF);
            // 亮色主题使用同一色相的较深版本，保持小字号对比度。
            r["HintActionBrush"] = B(0x66, 0x73, 0x8D);
            r["HintTransferBrush"] = B(0x80, 0x6A, 0x84);
            r["HintSearchBrush"] = B(0x8A, 0x73, 0x54);
            r["HintNavigationBrush"] = B(0x5F, 0x78, 0x86);
            r["HintFilterBrush"] = B(0x74, 0x6B, 0x91);
            r["HintManageBrush"] = B(0x91, 0x68, 0x74);
            r["HintExitBrush"] = B(0x92, 0x6A, 0x5B);
            r["ThemeBorder"] = B(0xBC, 0xC0, 0xCC);
            r["ThemeSeparator"] = B(0xCC, 0xD0, 0xDA);
            r["DangerBg"] = B(0xD2, 0x0F, 0x39);
            r["ScrollBarTrackBrush"] = B(0xE0, 0xE3, 0xEB);
            r["ScrollBarThumbBrush"] = B(0xB4, 0xB8, 0xC8);
            r["ScrollBarThumbHoverBrush"] = B(0x98, 0x9E, 0xB2);
        }
    }

    private static SolidColorBrush B(byte r, byte g, byte b) => new(Color.FromRgb(r, g, b));
    private static SolidColorBrush B(byte a, byte r, byte g, byte b) => new(Color.FromArgb(a, r, g, b));

    public static bool IsSystemDark()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int v && v == 0;
        }
        catch { return true; }
    }
}
