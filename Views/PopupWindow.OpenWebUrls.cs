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

        var requests = WebUrlLauncher.BuildRequests(
            entries.Where(entry => entry.Type == EntryType.Text)
                .Select(entry => new WebUrlLauncher.Candidate(entry.TextContent, entry.Source)));

        var opened = 0;
        foreach (var request in requests)
        {
            var result = WebUrlLauncher.Open(request);
            if (result.SourceBrowserError != null)
            {
                ClipboardDiagnosticsLog.Write(
                    $"open web URL source-browser fallback uri={request.Uri.AbsoluteUri} " +
                    $"browser={request.BrowserExecutable} error={result.SourceBrowserError.GetType().Name}: " +
                    result.SourceBrowserError.Message);
            }
            if (!result.Success)
            {
                ClipboardDiagnosticsLog.Write(
                    $"open web URL failed uri={request.Uri.AbsoluteUri} " +
                    $"error={result.DefaultBrowserError?.GetType().Name}: {result.DefaultBrowserError?.Message}");
                continue;
            }

            opened++;
            ClipboardDiagnosticsLog.Write(
                $"open web URL success uri={request.Uri.AbsoluteUri} route={result.Route} " +
                $"browser={request.BrowserExecutable ?? "default"}");
        }

        ClipboardDiagnosticsLog.Write(
            $"open selected web URLs selected={entries.Count} valid={requests.Count} opened={opened}");
        if (opened > 0 && !_popupPinned)
            HidePopup();
    }
}
