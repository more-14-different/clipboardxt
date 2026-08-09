using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using ClipboardManager.Models;
using Media = System.Windows.Media;
using Orientation = System.Windows.Controls.Orientation;

namespace ClipboardManager;

public partial class FileDialogJumpPickerWindow : Window
{
    private void JumpRowContextMenu_Opened(object sender, RoutedEventArgs e)
    {
        var row = ItemsList.SelectedItem as FileJumpPickerRow;
        CtxAddFavorite.Visibility = row is { IsFavorite: false } ? Visibility.Visible : Visibility.Collapsed;
        CtxRemoveFavorite.Visibility = row is { IsFavorite: true } ? Visibility.Visible : Visibility.Collapsed;
        CtxEditPhrase.Visibility = row is { IsFavorite: true } ? Visibility.Visible : Visibility.Collapsed;
        CtxRemoveRecentFolder.Visibility = row is { IsRecentFolder: true } ? Visibility.Visible : Visibility.Collapsed;
        CtxAddFavorite.InputGestureText = _settings.FileJumpFavoriteHotkeyDisplayName;
        CtxRemoveFavorite.InputGestureText = _settings.FileJumpFavoriteHotkeyDisplayName;
        CtxEditPhrase.InputGestureText = _settings.FileJumpEditPhraseHotkeyDisplayName;
        CtxRemoveRecentFolder.InputGestureText = _settings.FileJumpRemoveRecentHotkeyDisplayName;
    }

    private void CtxAddFavorite_Click(object sender, RoutedEventArgs e)
    {
        if (ItemsList.SelectedItem is not FileJumpPickerRow row || row.IsFavorite) return;
        var def = GuessPhraseFromPath(row.Path);
        var phrase = PromptSimpleText("收藏关键词（用于 everything 筛选）", def);
        if (phrase == null) return;
        phrase = phrase.Trim();
        if (string.IsNullOrEmpty(phrase)) phrase = def;

        _settings.FolderFavorites.RemoveAll(f =>
            string.Equals(f.Path, row.Path, StringComparison.OrdinalIgnoreCase));
        _settings.FolderFavorites.Add(new FolderFavoriteEntry { Phrase = phrase, Path = row.Path });
        _settings.Save();
        BuildMasterList();
        RefreshFilter();
        var i = _displayRows.ToList().FindIndex(r => string.Equals(r.Path, row.Path, StringComparison.OrdinalIgnoreCase));
        if (i >= 0) ItemsList.SelectedIndex = i;
    }

    private void CtxRemoveFavorite_Click(object sender, RoutedEventArgs e)
    {
        if (ItemsList.SelectedItem is not FileJumpPickerRow row || !row.IsFavorite) return;
        _settings.FolderFavorites.RemoveAll(f =>
            string.Equals(f.Path, row.Path, StringComparison.OrdinalIgnoreCase));
        _settings.Save();
        BuildMasterList();
        RefreshFilter();
    }

    private void ToggleFavoriteForCurrentSelection()
    {
        var rows = ItemsList.SelectedItems.Cast<FileJumpPickerRow>()
            .Where(r => _displayRows.Contains(r))
            .ToList();
        if (rows.Count == 0 && ItemsList.SelectedItem is FileJumpPickerRow selected)
            rows.Add(selected);
        ToggleFavoriteForRows(rows);
    }

    private void ToggleFavoriteForRows(IReadOnlyList<FileJumpPickerRow> rows)
    {
        var paths = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            if (string.IsNullOrWhiteSpace(row.Path)) continue;
            if (!seen.Add(row.Path)) continue;
            paths.Add(row.Path);
        }
        if (paths.Count == 0) return;

        var shouldFavorite = paths.Any(path => !_settings.FolderFavorites.Any(f =>
            string.Equals(f.Path, path, StringComparison.OrdinalIgnoreCase)));

        if (shouldFavorite)
        {
            foreach (var path in paths)
            {
                if (_settings.FolderFavorites.Any(f =>
                    string.Equals(f.Path, path, StringComparison.OrdinalIgnoreCase)))
                    continue;
                _settings.FolderFavorites.Add(new FolderFavoriteEntry
                {
                    Phrase = GuessPhraseFromPath(path),
                    Path = path
                });
            }
        }
        else
        {
            _settings.FolderFavorites.RemoveAll(f => paths.Any(path =>
                string.Equals(f.Path, path, StringComparison.OrdinalIgnoreCase)));
        }

        var keepPath = paths[0];
        _settings.Save();
        BuildMasterList();
        RefreshFilter();
        var i = _displayRows.ToList().FindIndex(r =>
            string.Equals(r.Path, keepPath, StringComparison.OrdinalIgnoreCase));
        if (i >= 0) ItemsList.SelectedIndex = i;
    }

    private void CtxEditPhrase_Click(object sender, RoutedEventArgs e)
        => EditPhraseForCurrentSelection();

    private void EditPhraseForCurrentSelection()
    {
        if (ItemsList.SelectedItem is not FileJumpPickerRow row || !row.IsFavorite) return;
        var phrase = PromptSimpleText("修改关键词", row.Phrase);
        if (phrase == null) return;
        phrase = phrase.Trim();
        var fav = _settings.FolderFavorites.FirstOrDefault(f =>
            string.Equals(f.Path, row.Path, StringComparison.OrdinalIgnoreCase));
        if (fav == null) return;
        fav.Phrase = phrase;
        _settings.Save();
        BuildMasterList();
        RefreshFilter();
        var i = _displayRows.ToList().FindIndex(r => string.Equals(r.Path, row.Path, StringComparison.OrdinalIgnoreCase));
        if (i >= 0) ItemsList.SelectedIndex = i;
    }

    private void CtxRemoveRecentFolder_Click(object sender, RoutedEventArgs e)
        => RemoveRecentForCurrentSelection();

    private void RemoveRecentForCurrentSelection()
    {
        if (ItemsList.SelectedItem is not FileJumpPickerRow row || !row.IsRecentFolder) return;
        _settings.RemoveRecentFileDialogFolder(row.Path);
        _collectorSnapshot.RemoveAll(c =>
            c.Label.StartsWith("常用", StringComparison.OrdinalIgnoreCase)
            && string.Equals(c.Path, row.Path, StringComparison.OrdinalIgnoreCase));
        BuildMasterList();
        RefreshFilter();
    }

    private static string GuessPhraseFromPath(string path)
    {
        try
        {
            var t = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return Path.GetFileName(t);
        }
        catch { return "收藏"; }
    }

    private string? PromptSimpleText(string title, string initial)
    {
        var dlg = new Window
        {
            Title = title,
            Owner = this,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Width = 400,
            SizeToContent = SizeToContent.Height,
            ResizeMode = ResizeMode.NoResize,
            ShowInTaskbar = false,
            Background = TryFindResource("WindowBgBrush") as Media.Brush ?? System.Windows.SystemColors.WindowBrush,
        };
        var tb = new System.Windows.Controls.TextBox
        {
            Text = initial,
            Margin = new Thickness(14, 6, 14, 8),
            FontSize = 13,
            Padding = new Thickness(8, 6, 8, 6),
            Background = TryFindResource("SurfaceBrush") as Media.Brush,
            Foreground = TryFindResource("PrimaryText") as Media.Brush,
            BorderBrush = TryFindResource("ThemeBorder") as Media.Brush,
            CaretBrush = TryFindResource("PrimaryText") as Media.Brush,
        };
        string? result = null;
        var ok = new System.Windows.Controls.Button { Content = "确定", Width = 88, Margin = new Thickness(0, 0, 8, 0), IsDefault = true };
        var cancel = new System.Windows.Controls.Button { Content = "取消", Width = 88, IsCancel = true };
        ok.Click += (_, _) => { result = tb.Text; dlg.DialogResult = true; };
        cancel.Click += (_, _) => { dlg.DialogResult = false; };
        var btnRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
            Margin = new Thickness(14, 4, 14, 14)
        };
        btnRow.Children.Add(ok);
        btnRow.Children.Add(cancel);
        var sp = new StackPanel();
        sp.Children.Add(new TextBlock
        {
            Text = title,
            Margin = new Thickness(14, 14, 14, 4),
            FontSize = 13,
            Foreground = TryFindResource("PrimaryText") as Media.Brush,
        });
        sp.Children.Add(tb);
        sp.Children.Add(btnRow);
        dlg.Content = sp;
        _suppressJumpHook = true;
        _suppressDismissForSubDialog = true;
        try
        {
            return dlg.ShowDialog() == true ? result?.Trim() : null;
        }
        finally
        {
            _suppressDismissForSubDialog = false;
            _suppressJumpHook = false;
        }
    }
}
