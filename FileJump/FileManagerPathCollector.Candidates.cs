using System.Diagnostics;
using System.IO;

namespace ClipboardManager;

/// <summary>
/// 枚举资源管理器 / Total Commander / XYplorer / Directory Opus 等窗口的路径；
/// 思路对齐 QuickSwitch 的 ShowMenu / Get_Zfolder。
/// </summary>
internal static partial class FileManagerPathCollector
{
    /// <summary>按 Z 序遍历顶层窗口，收集各文件管理器当前路径；末尾可附加「常用路径」。</summary>
    /// <param name="skipAlternateUiAutomation">为 true 时跳过白名单第三方管理器的 UIA 树扫描，用于先弹出跳转列表再异步补全。</param>
    /// <param name="stopAfterCandidateCount">大于 0 时，在去重后的候选条数达到该值后停止遍历。</param>
    /// <param name="shouldAbort">若返回 true 则立即停止顶层窗口遍历。</param>
    /// <param name="recentFolders">最近明确使用的目录；优先于单独的 <paramref name="memoryFolder"/>。</param>
    public static List<FileJumpCandidate> CollectCandidates(
        IntPtr dialogHwnd,
        string? memoryFolder,
        int zDelta = 2,
        bool skipAlternateUiAutomation = false,
        int stopAfterCandidateCount = 0,
        Func<bool>? shouldAbort = null,
        IReadOnlyList<string>? recentFolders = null)
    {
        var totalStopwatch = Stopwatch.StartNew();
        var stageStopwatch = Stopwatch.StartNew();
        var slowStages = new List<string>();
        var scannedTopLevel = 0;
        var explorerWindowCount = 0;
        var explorerResolveCount = 0;
        long shellEnumMs = 0;
        var shellEnumCacheHit = false;
        long explorerResolveMs = 0;
        long alternateUiMs = 0;
        var alternateUiCount = 0;
        var aborted = false;
        var topCap = -1;
        var executableByProcessId = new Dictionary<uint, string>();

        void RecordSlowStage(string name, long elapsedMs, long thresholdMs, string detail = "")
        {
            if (elapsedMs < thresholdMs) return;
            slowStages.Add(string.IsNullOrEmpty(detail)
                ? $"{name}={elapsedMs}ms"
                : $"{name}={elapsedMs}ms({detail})");
        }

        var candidates = new FileJumpCandidateAccumulator();

        void LogSummary(string exitReason)
        {
            totalStopwatch.Stop();
            if (totalStopwatch.ElapsedMilliseconds < 80 && slowStages.Count == 0) return;
            ClipboardDiagnosticsLog.Write(
                "filejump.perf collect_candidates " +
                $"elapsedMs={totalStopwatch.ElapsedMilliseconds} exit={exitReason} hwnd=0x{dialogHwnd.ToInt64():X} " +
                $"count={candidates.Count} added={candidates.AddedCount} scanned={scannedTopLevel} " +
                $"topCap={topCap} " +
                $"skipAlt={skipAlternateUiAutomation} stopAfter={stopAfterCandidateCount} " +
                $"explorerWindows={explorerWindowCount} explorerResolve={explorerResolveCount} " +
                $"shellEnumMs={shellEnumMs} shellCache={shellEnumCacheHit} explorerResolveMs={explorerResolveMs} " +
                $"altCount={alternateUiCount} altMs={alternateUiMs} " +
                $"aborted={aborted} slow=[{string.Join("; ", slowStages)}]");
        }

        stageStopwatch.Restart();
        var isQuickCollect = stopAfterCandidateCount > 0;
        if (TryGetZOrderLinkedFolder(
                dialogHwnd,
                zDelta,
                allowBlockingExplorerRefresh: !isQuickCollect,
                allowStaleExplorerCache: isQuickCollect,
                allowExplorerUiAutomation: !isQuickCollect,
                allowBlockingSpecializedManagers: !isQuickCollect) is { } zOrderHint)
            candidates.Add("Z 序推测", zOrderHint);
        RecordSlowStage("zorder_hint", stageStopwatch.ElapsedMilliseconds, 25);

        if (stopAfterCandidateCount > 0 && candidates.Count >= stopAfterCandidateCount)
        {
            candidates.AppendFavoriteFolders(memoryFolder, recentFolders);
            LogSummary("stop_after_zorder");
            return candidates.Result;
        }

        stageStopwatch.Restart();
        var topLevelWindows = GetTopLevelZOrderTopFirst();
        RecordSlowStage("top_level_enum", stageStopwatch.ElapsedMilliseconds, 25,
            $"count={topLevelWindows.Count}");
        var scanCap = topLevelWindows.Count;
        if (stopAfterCandidateCount > 0)
            scanCap = Math.Min(scanCap, 72);
        topCap = scanCap;

        List<ShellExplorerWindowEntry>? shellExplorerEntries = null;
        string? directoryOpusXml = null;
        for (var windowIndex = 0; windowIndex < scanCap; windowIndex++)
        {
            if (shouldAbort?.Invoke() == true)
            {
                aborted = true;
                break;
            }

            scannedTopLevel++;
            var window = topLevelWindows[windowIndex];
            var windowClass = Win32.GetWindowClassName(window);
            switch (windowClass)
            {
                case "TTOTAL_CMD":
                    if (isQuickCollect) break;
                    if (TryTotalCommanderPathFromClip(window, TcmCopySrcPathToClip, out var sourcePath))
                        candidates.Add("Total Commander (源)", sourcePath);
                    if (TryTotalCommanderPathFromClip(window, TcmCopyTrgPathToClip, out var targetPath))
                        candidates.Add("Total Commander (目标)", targetPath);
                    break;
                case "ThunderRT6FormDC":
                    if (isQuickCollect) break;
                    if (TryGetProcessImagePath(window, executableByProcessId, out var xyplorerExecutable)
                        && Path.GetFileNameWithoutExtension(xyplorerExecutable)
                            .Equals("xyplorer", StringComparison.OrdinalIgnoreCase))
                    {
                        if (TryXyplorerPathFromClip(window, "::copytext get('path', a);", out var xyplorerPath))
                            candidates.Add("XYplorer", xyplorerPath);
                    }
                    break;
                case "dopus.lister":
                    if (isQuickCollect) break;
                    directoryOpusXml ??= TryRunDopusInfoXml(window);
                    foreach (var (label, path) in ParseDopusListerPaths(directoryOpusXml, window))
                        candidates.Add(label, path);
                    break;
                case "CabinetWClass":
                case "ExploreWClass":
                {
                    if (shellExplorerEntries == null)
                    {
                        stageStopwatch.Restart();
                        shellExplorerEntries = TryEnumerateShellExplorerWindows(
                            out var cacheHit,
                            allowBlockingRefresh: !isQuickCollect,
                            allowStaleCache: isQuickCollect);
                        shellEnumMs += stageStopwatch.ElapsedMilliseconds;
                        shellEnumCacheHit = cacheHit;
                        explorerWindowCount = shellExplorerEntries.Count;
                        RecordSlowStage(
                            cacheHit ? "shell_windows_enum_cache" : "shell_windows_enum",
                            stageStopwatch.ElapsedMilliseconds,
                            cacheHit ? 8 : 40,
                            $"count={shellExplorerEntries.Count}");
                    }

                    stageStopwatch.Restart();
                    explorerResolveCount++;
                    if (TryGetExplorerPathForHwnd(
                            window,
                            shellExplorerEntries,
                            allowUiAutomation: !isQuickCollect) is { } explorerPath)
                        candidates.Add("资源管理器", explorerPath);
                    explorerResolveMs += stageStopwatch.ElapsedMilliseconds;
                    RecordSlowStage("explorer_resolve", stageStopwatch.ElapsedMilliseconds, 50,
                        $"hwnd=0x{window.ToInt64():X}");
                    break;
                }
                default:
                    if (skipAlternateUiAutomation) break;
                    if (!TryGetProcessImagePath(window, executableByProcessId, out var alternateExecutable)) break;
                    {
                        var processName = Path.GetFileNameWithoutExtension(alternateExecutable);
                        if (!ShouldUseAlternateUiAutomation(processName)) break;
                        var label = AlternateManagerDisplayLabel(processName, alternateExecutable);
                        if (processName.StartsWith("q-dir", StringComparison.OrdinalIgnoreCase))
                        {
                            stageStopwatch.Restart();
                            alternateUiCount++;
                            foreach (var path in CollectQDirFolderPathsFromAutomation(window))
                                candidates.Add(label, path);
                            alternateUiMs += stageStopwatch.ElapsedMilliseconds;
                            RecordSlowStage("alternate_qdir_uia", stageStopwatch.ElapsedMilliseconds, 60,
                                $"proc={processName}");
                        }
                        else
                        {
                            stageStopwatch.Restart();
                            alternateUiCount++;
                            if (TryFindBestFolderPathInAutomationTree(window, out var alternatePath))
                                candidates.Add(label, alternatePath);
                            alternateUiMs += stageStopwatch.ElapsedMilliseconds;
                            RecordSlowStage("alternate_uia", stageStopwatch.ElapsedMilliseconds, 60,
                                $"proc={processName}");
                        }
                    }
                    break;
            }

            if (stopAfterCandidateCount > 0 && candidates.Count >= stopAfterCandidateCount)
                break;
        }

        stageStopwatch.Restart();
        candidates.AppendFavoriteFolders(memoryFolder, recentFolders);
        RecordSlowStage("append_favorites", stageStopwatch.ElapsedMilliseconds, 25,
            $"recent={(recentFolders?.Count ?? 0)}");

        LogSummary("complete");
        return candidates.Result;
    }
}
