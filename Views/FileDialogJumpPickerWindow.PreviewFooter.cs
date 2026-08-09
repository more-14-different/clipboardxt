using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using ClipboardManager.Models;
using Media = System.Windows.Media;
using Orientation = System.Windows.Controls.Orientation;

namespace ClipboardManager;

public partial class FileDialogJumpPickerWindow : Window
{    private void CloseJumpPreviewBubble() => JumpPreviewPopup.IsOpen = false;

    private void ToggleJumpPreviewBubble()
    {
        FileJumpShortcutHelpPopup.IsOpen = false;
        if (_displayRows.Count == 0) return;
        if (ItemsList.SelectedItem is not FileJumpPickerRow) return;

        if (JumpPreviewPopup.IsOpen)
        {
            CloseJumpPreviewBubble();
            return;
        }

        ShowJumpPreviewBubble();
    }

    private void ShowJumpPreviewBubble()
    {
        if (ItemsList.SelectedItem is not FileJumpPickerRow row) return;
        UpdateJumpPreviewContent(row);
        PositionJumpPreviewPopup();
        JumpPreviewPopup.IsOpen = true;
    }

    private void SyncJumpPreviewWithSelection()
    {
        if (!JumpPreviewPopup.IsOpen) return;
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
        {
            if (!JumpPreviewPopup.IsOpen) return;
            if (ItemsList.SelectedItem is FileJumpPickerRow row)
            {
                UpdateJumpPreviewContent(row);
                PositionJumpPreviewPopup();
            }
            else
            {
                CloseJumpPreviewBubble();
            }
        });
    }

    private void UpdateJumpPreviewContent(FileJumpPickerRow row)
    {
        var primary = TryFindResource("PrimaryText") as Media.Brush ?? Media.Brushes.White;
        var accent = TryFindResource("AccentBg") as Media.Brush ?? Media.Brushes.Teal;
        var query = _searchText.Trim();

        var tags = row.BuildMetadataChips(SearchQuerySpec.Parse(query)).ToList();
        if (row.IsFavorite)
            tags.Insert(0, new SearchMetadataChip("收藏", false, true));
        JumpPreviewTags.ItemsSource = tags;
        JumpPreviewPath.Inlines.Clear();
        SearchHighlightInlines.Append(
            JumpPreviewPath.Inlines,
            row.Path,
            query,
            primary,
            accent,
            13,
            FontWeights.Normal);
    }

    private void PositionJumpPreviewPopup()
    {
        if (ItemsList.SelectedItem != null
            && ItemsList.ItemContainerGenerator.ContainerFromItem(ItemsList.SelectedItem) is ListBoxItem row
            && row.IsVisible)
        {
            JumpPreviewPopup.PlacementTarget = row;
            JumpPreviewPopup.Placement = System.Windows.Controls.Primitives.PlacementMode.Right;
            JumpPreviewPopup.HorizontalOffset = 10;
            JumpPreviewPopup.VerticalOffset = -16;
            return;
        }

        JumpPreviewPopup.PlacementTarget = MainBorder;
        JumpPreviewPopup.Placement = System.Windows.Controls.Primitives.PlacementMode.Right;
        JumpPreviewPopup.HorizontalOffset = 10;
        JumpPreviewPopup.VerticalOffset = 0;
    }

    private void UpdateFooterHints()
    {
        var m = PanelModifierDisplayName(_settings.PanelModifierKey);
        var primaryHints = new List<PanelHintItem>();
        PanelHintItem commitHint;
        PanelHintItem transferHint;

        if (_isStandaloneMode)
        {
            commitHint = new("Enter / Click", "打开", "在资源管理器中打开当前选中的文件夹。", PanelHintTone.Action);
            transferHint = new(
                "Ctrl+Enter / Ctrl+Click",
                "粘贴路径",
                "把所选文件夹的完整路径粘贴回打开面板前的原窗口。",
                PanelHintTone.Transfer);
        }
        else
        {
            commitHint = new(
                "Enter / Click",
                _autoForegroundStickyMode
                    ? "切换目录"
                    : "跳转",
                _autoForegroundStickyMode
                    ? "切换文件对话框目录；面板保持贴靠并继续跟随对话框。"
                    : "让当前文件对话框跳转到所选文件夹。",
                PanelHintTone.Action);
            transferHint = new(
                "Ctrl+Enter / Ctrl+Click",
                "复制路径",
                "复制所选文件夹的完整路径，不改变文件对话框目录。",
                PanelHintTone.Transfer);
        }

        primaryHints.Add(commitHint);
        primaryHints.Add(transferHint);
        PanelHintItem? numberHint = null;
        if (!_isStandaloneMode)
        {
            numberHint = new(
                $"{m}+1～9",
                "数字直达",
                "直接跳转到列表第 1～9 项，无需先移动选择。",
                PanelHintTone.Action);
            primaryHints.Add(numberHint);
        }

        var searchHint = new PanelHintItem(
            "输入 / 空格",
            "搜索",
            "搜索路径、关键词和来源；Everything 补充结果不支持拼音。",
            PanelHintTone.Search);
        primaryHints.Add(searchHint);

        var moveHint = new PanelHintItem("↑↓ / Ctrl+HJKL", "选择", "向上或向下移动当前选择。", PanelHintTone.Navigation);
        var edgeHint = new PanelHintItem(
            "Home / End",
            "首尾",
            "无搜索时移动到列表首项或末项；搜索时移动文本光标。",
            PanelHintTone.Navigation);
        var pageHint = new PanelHintItem("PgUp / PgDn", "翻页", "列表向上或向下翻页。", PanelHintTone.Navigation);
        var filterHint = new PanelHintItem("Tab", "过滤", "依次切换：全部 → 收藏 → 常用。", PanelHintTone.Filter);
        var favoriteHint = new PanelHintItem(
            _settings.FileJumpFavoriteHotkeyDisplayName,
            "收藏",
            "收藏或取消收藏当前文件夹。",
            PanelHintTone.Manage);
        var editHint = new PanelHintItem(
            _settings.FileJumpEditPhraseHotkeyDisplayName,
            "关键词",
            "修改当前文件夹用于搜索的自定义关键词。",
            PanelHintTone.Manage);
        var removeHint = new PanelHintItem(
            _settings.FileJumpRemoveRecentHotkeyDisplayName,
            "移除常用",
            "从常用路径中移除当前文件夹；不会删除磁盘内容。",
            PanelHintTone.Manage);
        var caretHint = new PanelHintItem(
            "←→ / Ctrl+←→",
            "搜索光标",
            "按字符或词段移动搜索光标；配合 Shift 可扩展选区。",
            PanelHintTone.Search);
        var detailHint = new PanelHintItem("F1", "详情", "显示当前文件夹的标签、来源和完整路径。", PanelHintTone.Filter);
        var menuHint = new PanelHintItem(
            "Right Click",
            "管理菜单",
            "打开收藏、关键词和常用路径管理菜单。",
            PanelHintTone.Manage);
        var escapeHint = new PanelHintItem(
            "Esc",
            "返回 / 关闭",
            "按层级关闭详情、清空搜索，最后关闭面板。",
            PanelHintTone.Exit);

        var coreItems = new List<PanelHintItem> { commitHint, transferHint };
        if (numberHint != null) coreItems.Add(numberHint);
        coreItems.Add(searchHint);

        var modeDescription = _isStandaloneMode
            ? "打开文件夹，或把完整路径送回原窗口。"
            : _autoForegroundStickyMode
                ? "为文件对话框切换目录；选择后面板仍保持跟随。"
                : "为当前文件对话框选择目标目录。";

        FileJumpPrimaryHints.ItemsSource = primaryHints;
        FooterHintsText.ItemsSource = new[]
        {
            moveHint,
            filterHint,
            favoriteHint,
            detailHint,
            escapeHint,
        };
        FileJumpShortcutHelpSubtitle.Text = modeDescription;
        FileJumpShortcutHelpSections.ItemsSource = new[]
        {
            new PanelHintSection("★", "核心操作", modeDescription, coreItems, PanelHintTone.Action),
            new PanelHintSection(
                "↕",
                "选择与浏览",
                "在列表与搜索文本中快速定位。",
                new[] { moveHint, edgeHint, pageHint, caretHint },
                PanelHintTone.Navigation),
            new PanelHintSection(
                "⌕",
                "筛选与查看",
                "缩小路径范围，并查看当前文件夹详情。",
                new[] { filterHint, detailHint },
                PanelHintTone.Filter),
            new PanelHintSection(
                "✦",
                "收藏与管理",
                "整理收藏、关键词与常用路径。",
                new[] { favoriteHint, editHint, removeHint, menuHint },
                PanelHintTone.Manage),
            new PanelHintSection(
                "×",
                "退出与恢复",
                "Esc 会优先撤销当前层级，不会立刻粗暴关窗。",
                new[] { escapeHint },
                PanelHintTone.Exit),
        };
    }

    private void FileJumpShortcutHelpMore_Click(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        CloseJumpPreviewBubble();
        FileJumpShortcutHelpPopup.IsOpen = !FileJumpShortcutHelpPopup.IsOpen;
    }

    private static string PanelModifierDisplayName(string key) => key switch
    {
        "Alt" => "Alt",
        "Win" => "Win",
        "CapsLock" => "CapsLk",
        _ => "Ctrl",
    };

    private bool IsPanelModifierMatch()
    {
        bool ctrl = (Win32.GetAsyncKeyState(0x11) & 0x8000) != 0;
        bool alt = (Win32.GetAsyncKeyState(0x12) & 0x8000) != 0;
        bool win = ((Win32.GetAsyncKeyState(0x5B) | Win32.GetAsyncKeyState(0x5C)) & 0x8000) != 0;
        bool caps = (Win32.GetAsyncKeyState(0x14) & 0x8000) != 0;

        return _settings.PanelModifierKey switch
        {
            "Alt" => alt && !ctrl,
            "Win" => win && !ctrl && !alt,
            "CapsLock" => caps && !ctrl && !alt,
            _ => ctrl && !alt,
        };
    }

    private bool IsStarToggleHotkeyMatch(uint vk)
    {
        return IsConfiguredHotkeyMatch(
            _settings.FileJumpFavoriteHotkeyModifiers,
            _settings.FileJumpFavoriteHotkeyKey,
            vk);
    }

    private static bool IsConfiguredHotkeyMatch(uint modifiers, uint key, uint vk)
    {
        if (vk != key) return false;

        bool ctrl = (Win32.GetAsyncKeyState(0x11) & 0x8000) != 0;
        bool shift = (Win32.GetAsyncKeyState(0x10) & 0x8000) != 0
            || (Win32.GetAsyncKeyState(0xA0) & 0x8000) != 0
            || (Win32.GetAsyncKeyState(0xA1) & 0x8000) != 0;
        bool alt = (Win32.GetAsyncKeyState(0x12) & 0x8000) != 0;
        bool win = ((Win32.GetAsyncKeyState(0x5B) | Win32.GetAsyncKeyState(0x5C)) & 0x8000) != 0;

        return ctrl == ((modifiers & Win32.MOD_CONTROL) != 0)
            && shift == ((modifiers & Win32.MOD_SHIFT) != 0)
            && alt == ((modifiers & Win32.MOD_ALT) != 0)
            && win == ((modifiers & Win32.MOD_WIN) != 0);
    }

    private void ToggleFavoritesFilter()
    {
        _filterMode = _filterMode switch
        {
            FileJumpPickerFilterMode.All => FileJumpPickerFilterMode.FavoritesOnly,
            FileJumpPickerFilterMode.FavoritesOnly => FileJumpPickerFilterMode.RecentOnly,
            _ => FileJumpPickerFilterMode.All
        };
        UpdateFilterModeUi();
        ResetSearchEditorState();
        var keepPath = (ItemsList.SelectedItem as FileJumpPickerRow)?.Path;
        RefreshFilter();
        if (!string.IsNullOrEmpty(keepPath))
        {
            var i = _displayRows.ToList().FindIndex(r => string.Equals(r.Path, keepPath, StringComparison.OrdinalIgnoreCase));
            if (i >= 0) ItemsList.SelectedIndex = i;
        }
    }

    private void FileJumpTypeFilter_Click(object sender, MouseButtonEventArgs e) => ToggleFavoritesFilter();

    private void UpdateFilterModeUi()
    {
        (FileJumpTypeFilterIcon.Text, FileJumpTypeFilterText.Text) = _filterMode switch
        {
            FileJumpPickerFilterMode.FavoritesOnly => ("★", "收藏"),
            FileJumpPickerFilterMode.RecentOnly => ("◷", "常用"),
            _ => ("●", "全部")
        };
    }
}

