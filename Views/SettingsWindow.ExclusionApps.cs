using System.Windows;

namespace ClipboardManager;

public partial class SettingsWindow : Window
{
    private void ReloadExclusionAppsList()
    {
        ExclusionAppsList.Items.Clear();
        foreach (var app in _pendingExclusionApps)
            ExclusionAppsList.Items.Add(app);
    }

    private void ExclusionAppAdd_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ProcessPickerDialog(this, ProcessNameCatalog.GetRunningProcessNames(Environment.ProcessId));
        dialog.ProcessSelected += AddExclusionAppIfMissing;
        dialog.ShowDialog();
    }

    private void AddExclusionAppIfMissing(string name)
    {
        if (_pendingExclusionApps.Contains(name, StringComparer.OrdinalIgnoreCase))
            return;

        _pendingExclusionApps.Add(name);
        ReloadExclusionAppsList();
    }

    private void ExclusionAppDelete_Click(object sender, RoutedEventArgs e)
    {
        if (ExclusionAppsList.SelectedItem is string selected)
        {
            _pendingExclusionApps.RemoveAll(a => string.Equals(a, selected, StringComparison.OrdinalIgnoreCase));
            ReloadExclusionAppsList();
        }
    }
}

