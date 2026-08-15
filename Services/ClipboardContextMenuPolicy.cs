using ClipboardManager.Models;

namespace ClipboardManager;

internal static class ClipboardContextMenuPolicy
{
    internal readonly record struct State(
        bool ShowLinePaste,
        bool ShowSoftLinePaste,
        bool ShowOpenUrls,
        bool ShowPasteAsFile);

    internal static State Evaluate(
        IReadOnlyList<ClipboardEntry> selectedEntries,
        BatchPasteQueueMode batchMode)
    {
        var isMultiText = selectedEntries.Count > 1
            && selectedEntries.All(entry => entry.Type == EntryType.Text);
        var showTextPostProcessing = batchMode == BatchPasteQueueMode.Off && isMultiText;
        var showOpenUrls = WebUrlLauncher.BuildRequests(
            selectedEntries
                .Where(entry => entry.Type == EntryType.Text)
                .Select(entry => new WebUrlLauncher.Candidate(entry.TextContent, entry.Source)))
            .Count > 0;

        return new State(
            ShowLinePaste: showTextPostProcessing,
            ShowSoftLinePaste: showTextPostProcessing,
            ShowOpenUrls: showOpenUrls,
            ShowPasteAsFile: selectedEntries.Any(ClipboardFileExportPlanner.CanExport));
    }
}
