using System.Windows;

namespace ClipboardManager;

public partial class PopupWindow : Window
{
    private void OpenSelectedWebUrls()
    {
        var entries = ItemsList.SelectedItems.Cast<ClipboardEntry>()
            .Where(entry => _displayItems.Contains(entry))
            .OrderBy(entry => _displayItems.IndexOf(entry))
            .ToList();
        if (entries.Count == 0
            && ItemsList.SelectedItem is ClipboardEntry selected
            && _displayItems.Contains(selected))
        {
            entries.Add(selected);
        }

        var urls = WebUrlLauncher.CollectUnique(
            entries.Where(entry => entry.Type == EntryType.Text)
                .Select(entry => entry.TextContent));

        var opened = 0;
        foreach (var uri in urls)
        {
            try
            {
                WebUrlLauncher.Open(uri);
                opened++;
            }
            catch (Exception ex)
            {
                ClipboardDiagnosticsLog.Write(
                    $"open web URL failed uri={uri.AbsoluteUri} error={ex.GetType().Name}: {ex.Message}");
            }
        }

        ClipboardDiagnosticsLog.Write(
            $"open selected web URLs selected={entries.Count} valid={urls.Count} opened={opened}");
        if (opened > 0 && !_popupPinned)
            HidePopup();
    }
}
