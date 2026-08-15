using System.IO;
using System.Text;
using ClipboardManager.Services;

namespace ClipboardManager;

public partial class PopupWindow
{
    private async void PasteSelectedItemsAsFilesForExplorer(ClipboardEntry? contextEntry = null)
    {
        var entries = GetOrderedEntriesForFileExport(contextEntry);
        var plan = ClipboardFileExportPlanner.Build(entries);
        if (plan.Count == 0) return;

        _pasteInProgress = true;
        var createdTempPaths = new List<string>();
        try
        {
            ClearPendingDelete();
            TouchFileExportEntries(entries);

            var tempDirectory = Path.Combine(Path.GetTempPath(), "ClipboardX");
            Directory.CreateDirectory(tempDirectory);
            var timestamp = DateTime.Now;
            var batchToken = Guid.NewGuid().ToString("N")[..8];
            var paths = new List<string>(plan.Count);
            var generatedIndex = 0;

            foreach (var item in plan)
            {
                if (item.Kind == ClipboardFileExportPlanner.ItemKind.ExistingPath)
                {
                    if (!string.IsNullOrWhiteSpace(item.ExistingPath))
                        paths.Add(item.ExistingPath);
                    continue;
                }

                generatedIndex++;
                var fileName = ClipboardFileExportPlanner.BuildTempFileName(
                    timestamp,
                    batchToken,
                    generatedIndex,
                    item.Kind);
                var path = Path.Combine(tempDirectory, fileName);
                try
                {
                    switch (item.Kind)
                    {
                        case ClipboardFileExportPlanner.ItemKind.Text:
                        case ClipboardFileExportPlanner.ItemKind.Json:
                            File.WriteAllText(path, item.Text ?? "", new UTF8Encoding(false));
                            break;
                        case ClipboardFileExportPlanner.ItemKind.Png:
                            File.WriteAllBytes(
                                path,
                                ClipboardImageCodec.NormalizePngBytes(item.ImageData ?? []));
                            break;
                    }

                    paths.Add(path);
                    createdTempPaths.Add(path);
                }
                catch (Exception ex)
                {
                    ClipboardDiagnosticsLog.Write(
                        $"paste as files temp write failed kind={item.Kind} file=\"{path}\" " +
                        $"error={ex.GetType().Name}: {ex.Message}");
                    try { File.Delete(path); } catch { /* ignore */ }
                }
            }

            if (paths.Count == 0) return;
            await CompletePasteFilesToExplorerAsync(
                paths,
                createdTempPaths,
                $"entries={entries.Count} files={paths.Count} generated={createdTempPaths.Count}",
                $"SetFileDropList explorer_export_batch count={paths.Count}");
        }
        catch (Exception ex)
        {
            ClipboardDiagnosticsLog.Write(
                $"paste selected items as files failed: {ex.GetType().Name}: {ex.Message}");
            foreach (var path in createdTempPaths)
            {
                try { File.Delete(path); } catch { /* ignore */ }
            }
        }
        finally
        {
            _pasteInProgress = false;
        }
    }

    private List<ClipboardEntry> GetOrderedEntriesForFileExport(ClipboardEntry? contextEntry)
    {
        var entries = ItemsList.SelectedItems.Cast<ClipboardEntry>()
            .Where(entry => _displayItems.Contains(entry))
            .Distinct()
            .OrderBy(entry => _displayItems.IndexOf(entry))
            .ToList();

        if (contextEntry != null && !entries.Contains(contextEntry))
            return _displayItems.Contains(contextEntry) ? [contextEntry] : [];
        if (entries.Count == 0
            && ItemsList.SelectedItem is ClipboardEntry selected
            && _displayItems.Contains(selected))
        {
            entries.Add(selected);
        }

        return entries;
    }

    private void TouchFileExportEntries(IReadOnlyList<ClipboardEntry> entries)
    {
        for (var i = entries.Count - 1; i >= 0; i--)
        {
            var entry = entries[i];
            if (entry.IsQuickPaste) continue;
            if (entry.IsArchived)
                _historyStore.TryRestoreArchived(entry);
            var currentIndex = _allItems.IndexOf(entry);
            if (currentIndex >= 0)
            {
                _allItems.RemoveAt(currentIndex);
                _allItems.Insert(0, entry);
            }
            entry.TouchCopiedTime();
            if (entry.PersistedId is long id)
                _historyStore.TryUpdateCopiedAt(id, entry.CopiedAt);
        }
    }
}
