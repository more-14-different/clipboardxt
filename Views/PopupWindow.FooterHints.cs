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
    private void UpdateFooterHints()
    {
        string m = PanelModifierDisplayName;
        string pageUpFull = AppSettings.FormatHotkey(_panelPageScrollUpModifiers, _panelPageScrollUpKey);
        string pageDnFull = AppSettings.FormatHotkey(_panelPageScrollDownModifiers, _panelPageScrollDownKey);

        var pasteHint = new PanelHintItem(
            _appSettings?.ClipboardPasteHotkeyDisplayName ?? "Enter",
            "粘贴",
            "粘贴当前条目；有队列时优先粘贴队首。普通模式多选会顺序连贴，FIFO/LIFO 多选会入队；在其它窗口按 Ctrl+V 或 Shift+Insert 可逐条出队。",
            PanelHintTone.Action);
        var quickPasteHint = new PanelHintItem(
            $"{m}+1～9",
            "数字快贴",
            $"按住面板主键 {m}，直接粘贴列表第 1～9 条。",
            PanelHintTone.Action);
        var searchHint = new PanelHintItem(
            "输入 / 空格",
            "搜索",
            "搜索剪贴板内容；空格分词按 AND 匹配，也可检索来源与快捷短语。",
            PanelHintTone.Search);

        var moveHint = new PanelHintItem("↑↓ / Ctrl+HJKL", "选择", "移动当前焦点；已点选的多选项会继续保留。", PanelHintTone.Navigation);
        var edgeHint = new PanelHintItem("Home / End", "首尾", "移动到列表首项或末项。", PanelHintTone.Navigation);
        var pageHint = new PanelHintItem(
            $"{pageUpFull} / {pageDnFull}",
            "翻页",
            "列表向上或向下翻页；快捷键可在设置中修改。",
            PanelHintTone.Navigation);
        var pointHint = new PanelHintItem(
            "Ctrl+Enter",
            "点选",
            "点选或取消当前条目；之后移动焦点仍保留已选项，最后按粘贴键执行。",
            PanelHintTone.Navigation);
        var selectionItems = new List<PanelHintItem> { moveHint, edgeHint, pageHint, pointHint };

        var phraseFilterHint = new PanelHintItem(
            $"{m}+Tab",
            "仅看短语",
            "只显示快捷短语；再按一次恢复完整列表。",
            PanelHintTone.Filter);
        var typeFilterHint = new PanelHintItem("Tab", "类型筛选", "循环切换全部、文本、图片、文件等类型。", PanelHintTone.Filter);
        var caretHint = new PanelHintItem(
            "←→ / Ctrl+←→",
            "搜索光标",
            "按字符或词段移动搜索光标；配合 Shift 可扩展选区。",
            PanelHintTone.Search);
        var filterItems = new List<PanelHintItem> { phraseFilterHint, typeFilterHint, caretHint };

        var linePasteHint = new PanelHintItem(
            "Alt+Enter",
            "换行连贴",
            "普通模式多选顺序连贴时，在每条文本末尾附带换行。",
            PanelHintTone.Action);
        var openUrlHint = new PanelHintItem(
            "Alt+Shift+Enter",
            "打开网址",
            "打开当前文本中的合法 http/https 网址；来源是浏览器时优先用来源浏览器，否则使用默认浏览器。",
            PanelHintTone.Action);
        var softLinePasteHint = new PanelHintItem(
            "Shift+Enter",
            "软换行连贴",
            "普通模式多选顺序连贴时，在每条文本后发送软换行。",
            PanelHintTone.Action);
        var favoriteHint = new PanelHintItem(
            _appSettings?.StarToggleHotkeyDisplayName ?? "Ctrl+D",
            "收藏",
            "收藏或取消收藏当前条目；多选时批量切换。",
            PanelHintTone.Manage);
        var deleteHint = new PanelHintItem(
            $"{_appSettings?.ClipboardDeleteHotkeyDisplayName ?? "Delete"} × 2",
            "删除",
            "连续按两次删除当前条目；第一次显示删除线，第二次确认。",
            PanelHintTone.Manage);
        var menuHint = new PanelHintItem(
            "Alt",
            "操作菜单",
            "有队列时打开批量一次性粘贴菜单；否则打开当前条目的操作菜单。",
            PanelHintTone.Manage);
        var manageItems = new List<PanelHintItem>
        {
            openUrlHint,
            linePasteHint,
            softLinePasteHint,
            favoriteHint,
            deleteHint,
            menuHint,
        };

#if CLIPX_CLIPBOARD
        var previewHint = new PanelHintItem("F1 / 中键", "预览", "预览当前条目的全文、图片或文件详情。", PanelHintTone.Filter);
        filterItems.Add(previewHint);
        var rangeHint = new PanelHintItem(
            "Shift+↑↓",
            "扩展多选",
            "扩展连续选区；FIFO/LIFO 模式中新复制内容会按当前模式自动入队，并显示队列角标、置顶队列项。",
            PanelHintTone.Navigation);
        selectionItems.Add(rangeHint);
        var batchCy = _appSettings?.BatchModeCycleHotkeyDisplayName ?? "Alt+/";
        var batchModeHint = new PanelHintItem(
            batchCy,
            "批量模式",
            "循环切换普通 → LIFO → FIFO；与顶栏批量标签左键操作相同，可在设置中修改快捷键。",
            PanelHintTone.Manage);
        manageItems.Add(batchModeHint);
#endif

        var escapeHint = new PanelHintItem(
            "Esc",
            "返回 / 关闭",
            "按层级关闭菜单或预览、撤销删除线、清空搜索，最后关闭面板。",
            PanelHintTone.Exit);
        var primaryHints = new[] { pasteHint, quickPasteHint, searchHint };
        var footerHints = new List<PanelHintItem> { moveHint, typeFilterHint, favoriteHint };
#if CLIPX_CLIPBOARD
        footerHints.Add(previewHint);
#endif
        footerHints.Add(deleteHint);
        footerHints.Add(escapeHint);

        PrimaryHints.ItemsSource = primaryHints;
        FooterHints.ItemsSource = footerHints;
        ShortcutHelpSubtitle.Text = $"先看核心操作；其余按任务分组。当前面板主键：{m}。";
        ShortcutHelpSections.ItemsSource = new[]
        {
            new PanelHintSection(
                "★",
                "核心操作",
                "粘贴、数字快贴和搜索是最高频入口。",
                primaryHints,
                PanelHintTone.Action),
            new PanelHintSection(
                "↕",
                "选择与浏览",
                "移动焦点、翻页与构建多选。",
                selectionItems,
                PanelHintTone.Navigation),
            new PanelHintSection(
                "⌕",
                "筛选与预览",
                "缩小结果范围，并查看条目完整内容。",
                filterItems,
                PanelHintTone.Filter),
            new PanelHintSection(
                "✦",
                "批量与管理",
                "控制连贴方式、收藏、删除和操作菜单。",
                manageItems,
                PanelHintTone.Manage),
            new PanelHintSection(
                "×",
                "退出与恢复",
                "Esc 会优先撤销当前层级，再关闭面板。",
                new[] { escapeHint },
                PanelHintTone.Exit),
        };
    }

    private void ShortcutHelpMore_Click(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        ShortcutHelpPopup.IsOpen = !ShortcutHelpPopup.IsOpen;
    }
}
