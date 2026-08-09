using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Button = System.Windows.Controls.Button;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using ListBox = System.Windows.Controls.ListBox;
using Orientation = System.Windows.Controls.Orientation;
using TextBox = System.Windows.Controls.TextBox;

namespace ClipboardManager;

public sealed class ProcessPickerDialog : Window
{
    private readonly IReadOnlyList<string> _processes;
    private readonly TextBox _searchBox;
    private readonly Border _searchPlaceholder;
    private readonly ListBox _processList;
    private readonly Button _okButton;

    public event Action<string>? ProcessSelected;

    public ProcessPickerDialog(Window owner, IReadOnlyList<string> processes)
    {
        _processes = processes;

        var windowBg = (Brush)owner.FindResource("WindowBgBrush");
        var surface = (Brush)owner.FindResource("SurfaceBrush");
        var primaryText = (Brush)owner.FindResource("PrimaryText");
        var borderBrush = (Brush)owner.FindResource("ThemeBorder");

        Title = "添加排除应用";
        Width = 400;
        Height = 450;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Owner = owner;
        ResizeMode = ResizeMode.NoResize;
        Background = windowBg;

        var grid = new Grid { Margin = new Thickness(12) };
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(8) });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(8) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        _searchBox = new TextBox
        {
            Background = surface,
            Foreground = primaryText,
            BorderBrush = borderBrush,
            Padding = new Thickness(6, 4, 6, 4)
        };

        var searchLabel = new TextBlock
        {
            Text = "搜索进程名…",
            Foreground = primaryText,
            Opacity = 0.4,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 0, 0),
            IsHitTestVisible = false
        };
        _searchPlaceholder = new Border
        {
            Background = Brushes.Transparent,
            Child = searchLabel,
            IsHitTestVisible = false
        };

        var searchHost = new Grid();
        searchHost.Children.Add(_searchBox);
        searchHost.Children.Add(_searchPlaceholder);
        Grid.SetRow(searchHost, 0);

        _processList = new ListBox
        {
            Background = surface,
            Foreground = primaryText,
            BorderBrush = borderBrush,
            BorderThickness = new Thickness(1)
        };
        Grid.SetRow(_processList, 2);

        _okButton = new Button
        {
            Content = "添加选中",
            Padding = new Thickness(16, 4, 16, 4),
            MinWidth = 90,
            Background = surface,
            Foreground = primaryText,
            BorderBrush = borderBrush,
            IsEnabled = false
        };
        var cancelButton = new Button
        {
            Content = "关闭",
            Padding = new Thickness(16, 4, 16, 4),
            MinWidth = 80,
            Margin = new Thickness(8, 0, 0, 0),
            Background = surface,
            Foreground = primaryText,
            BorderBrush = borderBrush
        };
        var btnPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Right
        };
        btnPanel.Children.Add(_okButton);
        btnPanel.Children.Add(cancelButton);
        Grid.SetRow(btnPanel, 4);

        grid.Children.Add(searchHost);
        grid.Children.Add(_processList);
        grid.Children.Add(btnPanel);
        Content = grid;

        _searchBox.TextChanged += (_, _) =>
        {
            _searchPlaceholder.Visibility = string.IsNullOrEmpty(_searchBox.Text)
                ? Visibility.Visible
                : Visibility.Collapsed;
            RefreshList(_searchBox.Text);
        };
        _processList.SelectionChanged += (_, _) => _okButton.IsEnabled = _processList.SelectedItem != null;
        _okButton.Click += (_, _) => AddSelected();
        cancelButton.Click += (_, _) => DialogResult = false;
        PreviewKeyDown += OnPreviewKeyDown;

        RefreshList(null);
    }

    private void RefreshList(string? filter)
    {
        _processList.Items.Clear();
        foreach (var name in ProcessNameCatalog.Filter(_processes, filter))
            _processList.Items.Add(name);
    }

    private void AddSelected()
    {
        if (_processList.SelectedItem is not string name)
            return;

        ProcessSelected?.Invoke(name);
        _searchBox.Clear();
        _searchBox.Focus();
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Return && _processList.SelectedItem != null)
        {
            AddSelected();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            DialogResult = false;
            e.Handled = true;
        }
    }
}
