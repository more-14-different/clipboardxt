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
    private async Task PasteMultipleSelectedInOrderAsync(
        bool newlineAfterEachTextWhenAltEnter = false,
        bool softLineBreakAfterEachTextWhenShiftEnter = false)
    {
        var ordered = ItemsList.SelectedItems.Cast<ClipboardEntry>()
            .Where(e => _displayItems.Contains(e))
            .OrderBy(e => _displayItems.IndexOf(e))
            .ToList();
        if (ordered.Count == 0) return;
        ClearSearchText();
        if (await TryPasteEntriesIntoFileJumpSearchAsync(
                ordered,
                newlineAfterEachTextWhenAltEnter || softLineBreakAfterEachTextWhenShiftEnter))
            return;
        if (ordered.Count == 1)
        {
            await PasteEntryAsync(ordered[0], hidePopupAfter: true);
            return;
        }

        if (softLineBreakAfterEachTextWhenShiftEnter)
        {
            await RunSequentialPastesAsync(
                ordered,
                newlineAfterEachTextWhenAltEnter: false,
                softLineBreakAfterEachTextWhenShiftEnter: true);
            return;
        }

        var mergeText = _appSettings?.BatchPasteMergeText ?? true;
        if (IsAllTextEntries(ordered) && mergeText)
            await RunAllTextBatchSingleClipboardAsync(ordered, newlineAfterEachTextWhenAltEnter);
        else if (mergeText)
            await RunOrderedPastesWithAdjacentTextMergeAsync(ordered, newlineAfterEachTextWhenAltEnter);
        else
            await RunSequentialPastesAsync(ordered, newlineAfterEachTextWhenAltEnter);
    }

    private static bool IsAllTextEntries(IReadOnlyList<ClipboardEntry> items) =>
        items.Count > 0 && items.All(e => e.Type == EntryType.Text);

    /// <summary>保持顺序将列表按「Text vs 非 Text」二分聚合：相邻 Text 归一段；相邻 Image/Files 归一段（统一以 FileDropList 一次粘贴）。</summary>
    private static List<List<ClipboardEntry>> BuildAdjacentRuns(IReadOnlyList<ClipboardEntry> ordered)
    {
        static bool SameKind(EntryType a, EntryType b) =>
            (a == EntryType.Text) == (b == EntryType.Text);
        var segments = new List<List<ClipboardEntry>>();
        var i = 0;
        while (i < ordered.Count)
        {
            var anchor = ordered[i].Type;
            var run = new List<ClipboardEntry> { ordered[i] };
            i++;
            while (i < ordered.Count && SameKind(anchor, ordered[i].Type))
            {
                run.Add(ordered[i]);
                i++;
            }
            segments.Add(run);
        }
        return segments;
    }

    private static bool IsAllImageOrFilesEntries(IReadOnlyList<ClipboardEntry> items) =>
        items.Count > 0 && items.All(e => e.Type == EntryType.Image || e.Type == EntryType.Files);
}
