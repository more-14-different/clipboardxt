using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using ClipboardManager.Models;

namespace ClipboardManager;

public partial class FileDialogJumpPickerWindow : Window
{
    private void ApplyNavigateKeepOpenListRefresh(string committedPath, List<FileJumpCandidate> fresh)
    {
        var swTotal = Stopwatch.StartNew();
        _collectorSnapshot.Clear();
        _collectorSnapshot.AddRange(fresh);

        BuildMasterList();
        RefreshFilter(scrollSelection: false);
        var i = _displayRows.ToList().FindIndex(r =>
            string.Equals(r.Path, committedPath, StringComparison.OrdinalIgnoreCase));
        if (i >= 0)
        {
            ItemsList.SelectedIndex = i;
            if (_displayRows.Count > PageSize)
                ItemsList.ScrollIntoView(ItemsList.SelectedItem);
        }
        else if (_displayRows.Count > 0)
        {
            ItemsList.SelectedIndex = 0;
            if (_displayRows.Count > PageSize)
                ItemsList.ScrollIntoView(ItemsList.SelectedItem);
        }

        if (_dockBesideDialog && _hwnd != IntPtr.Zero)
        {
            Dispatcher.BeginInvoke(() =>
            {
                try
                {
                    var sw = Stopwatch.StartNew();
                    UpdateDockPopupPhysicalSizeCache();
                    TryRealtimeDockFollow(force: true);
                    PerfLog("navigate_refresh_realign", sw.ElapsedMilliseconds, 8);
                }
                catch { /* ignore */ }
            }, DispatcherPriority.Background);
        }
        swTotal.Stop();
        PerfLog("navigate_refresh_total", swTotal.ElapsedMilliseconds, 35,
            $"fresh={fresh.Count} display={_displayRows.Count}");
    }
}
