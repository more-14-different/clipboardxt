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
    private void UpdateBatchHeaderUi()
    {
        ApplyBatchModeChromeResources();
        if (BatchModeHeaderText == null) return;
        BatchModeHeaderText.Text = GetBatchMode() switch
        {
            BatchPasteQueueMode.Fifo => "FIFO",
            BatchPasteQueueMode.Lifo => "LIFO",
            _ => "普通"
        };
    }

    /// <summary>顶栏模式 Tag、列表队列角标、列表选中/悬停与托盘图标共用 <see cref="TrayIconSvg"/> 模式主色。</summary>
    private void ApplyBatchModeChromeResources()
    {
        var mode = GetBatchMode();
        var fill = HexToFrozenBrush(TrayIconSvg.GetModeMainHex(mode));
        Resources["BatchModeTagFill"] = fill;
        Resources["BatchModeBadgeFill"] = fill;
        Resources["KeyboardPointFocusBrush"] = fill;
        Resources["BatchModeTagFg"] = System.Windows.Media.Brushes.White;
        Resources["BatchModeBadgeFg"] = System.Windows.Media.Brushes.White;
        ApplyListSelectionBrushesForMode(mode);
    }

    private void ApplyListSelectionBrushesForMode(BatchPasteQueueMode mode)
    {
        var (mr, mg, mb) = ParseHexRgb(TrayIconSvg.GetModeMainHex(mode));
        if (IsDarkThemeEffective())
        {
            Resources["HoverBrush"] = MixRgbOnDarkEditor(mr, mg, mb, 7, 18);
            Resources["SelectedBrush"] = MixRgbOnDarkEditor(mr, mg, mb, 12, 13);
        }
        else
        {
            Resources["HoverBrush"] = MixRgbOnLightWindow(mr, mg, mb, 5, 20);
            Resources["SelectedBrush"] = MixRgbOnLightWindow(mr, mg, mb, 10, 15);
        }
    }

    private bool IsDarkThemeEffective()
    {
        if (_appSettings == null) return ThemeManager.IsSystemDark();
        return _appSettings.Theme switch
        {
            "Dark" => true,
            "Light" => false,
            _ => ThemeManager.IsSystemDark()
        };
    }

    private static (byte R, byte G, byte B) ParseHexRgb(string hex)
    {
        hex = hex.TrimStart('#');
        if (hex.Length != 6) return (0x13, 0x94, 0x93);
        return (
            Convert.ToByte(hex[..2], 16),
            Convert.ToByte(hex[2..4], 16),
            Convert.ToByte(hex[4..6], 16));
    }

    /// <summary>与 <see cref="ThemeManager"/> 暗色列表混合比例一致，仅前景换为当前模式主色。</summary>
    private static SolidColorBrush MixRgbOnDarkEditor(byte r, byte g, byte b, int wFg, int wBg)
    {
        const byte bg = 0x1E;
        return MixRgbOnSolid(r, g, b, bg, bg, bg, wFg, wBg);
    }

    /// <summary>亮色窗口底 <see cref="ThemeManager"/> 浅灰混模式主色。</summary>
    private static SolidColorBrush MixRgbOnLightWindow(byte r, byte g, byte b, int wFg, int wBg)
    {
        return MixRgbOnSolid(r, g, b, 0xEF, 0xF1, 0xF5, wFg, wBg);
    }

    private static SolidColorBrush MixRgbOnSolid(
        byte r, byte g, byte b,
        byte bgR, byte bgG, byte bgB,
        int wFg, int wBg)
    {
        var d = wFg + wBg;
        if (d <= 0)
            d = 1;
        byte M(byte f, byte bg) => (byte)((f * (long)wFg + bg * (long)wBg) / d);
        var brush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(M(r, bgR), M(g, bgG), M(b, bgB)));
        brush.Freeze();
        return brush;
    }

    private static SolidColorBrush HexToFrozenBrush(string hex)
    {
        var c = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex)!;
        var b = new SolidColorBrush(c);
        b.Freeze();
        return b;
    }
}
