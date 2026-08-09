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
    /// <summary>
    /// 外部文件管理器路径变化后，刷新当前候选列表并尽量保持用户关注的路径选中。
    /// </summary>
    public void RefreshCandidatesFromExternal(IReadOnlyList<FileJumpCandidate> fresh, string? preferredPath = null)
    {
        var freshList = fresh as List<FileJumpCandidate> ?? fresh.ToList();
        if (string.IsNullOrEmpty(preferredPath) && CandidateListsEquivalent(_collectorSnapshot, freshList))
        {
            ClipboardDiagnosticsLog.Write(
                $"filejump.perf skip_external_refresh_same fresh={freshList.Count} moveActive={_dockOwnerMoveActive}");
            return;
        }

        _deferredExternalRefresh = freshList;
        _deferredExternalPreferredPath = preferredPath;
        var delayMs = _dockOwnerMoveActive ? 220 : 90;
        ClipboardDiagnosticsLog.Write(
            $"filejump.perf defer_external_refresh moveActive={_dockOwnerMoveActive} delayMs={delayMs} fresh={freshList.Count} preferred={(string.IsNullOrEmpty(preferredPath) ? 0 : 1)}");
        ScheduleDeferredExternalRefresh(delayMs);
    }

    private void ApplyExternalRefreshNow(List<FileJumpCandidate> fresh, string? preferredPath)
    {
        if (string.IsNullOrEmpty(preferredPath) && CandidateListsEquivalent(_collectorSnapshot, fresh))
        {
            ClipboardDiagnosticsLog.Write(
                $"filejump.perf skip_external_refresh_same fresh={fresh.Count} moveActive={_dockOwnerMoveActive}");
            return;
        }
        var sw = Stopwatch.StartNew();
        var selectedPath = preferredPath;
        if (string.IsNullOrEmpty(selectedPath) && ItemsList.SelectedItem is FileJumpPickerRow row)
            selectedPath = row.Path;
        ApplyNavigateKeepOpenListRefresh(selectedPath ?? "", fresh);
        sw.Stop();
        PerfLog("refresh_candidates_external", sw.ElapsedMilliseconds, 25,
            $"fresh={fresh.Count} preferred={(string.IsNullOrEmpty(preferredPath) ? 0 : 1)}");
    }

    private void FlushDeferredExternalRefresh()
    {
        if (_deferredExternalRefresh == null) return;
        ScheduleDeferredExternalRefresh(220);
    }

    private void ScheduleDeferredExternalRefresh(int delayMs)
    {
        _deferredExternalRefreshTimer?.Stop();
        _deferredExternalRefreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(delayMs)
        };
        _deferredExternalRefreshTimer.Tick += (_, _) =>
        {
            _deferredExternalRefreshTimer?.Stop();
            _deferredExternalRefreshTimer = null;
            if (!IsLoaded) return;
            if (_dockOwnerMoveActive)
            {
                ScheduleDeferredExternalRefresh(220);
                return;
            }
            if (_deferredExternalRefresh == null) return;

            var fresh = _deferredExternalRefresh;
            var preferredPath = _deferredExternalPreferredPath;
            _deferredExternalRefresh = null;
            _deferredExternalPreferredPath = null;
            Dispatcher.BeginInvoke(() =>
            {
                if (!IsLoaded || _dockOwnerMoveActive)
                {
                    _deferredExternalRefresh = fresh;
                    _deferredExternalPreferredPath = preferredPath;
                    ScheduleDeferredExternalRefresh(220);
                    return;
                }

                ApplyExternalRefreshNow(fresh, preferredPath);
            }, DispatcherPriority.ContextIdle);
        };
        _deferredExternalRefreshTimer.Start();
    }

    private static bool CandidateListsEquivalent(IReadOnlyList<FileJumpCandidate> current, IReadOnlyList<FileJumpCandidate> fresh)
    {
        if (current.Count != fresh.Count) return false;
        for (int i = 0; i < current.Count; i++)
        {
            if (!string.Equals(current[i].Path, fresh[i].Path, StringComparison.OrdinalIgnoreCase))
                return false;
            if (!string.Equals(current[i].Label, fresh[i].Label, StringComparison.Ordinal))
                return false;
        }

        return true;
    }
}
