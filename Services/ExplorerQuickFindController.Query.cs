using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace ClipboardManager;

public sealed partial class ExplorerQuickFindController : IDisposable
{    private void ScheduleQuery()
    {
        _queryGen++;
        var gen = _queryGen;
        _queryCts?.Cancel();
        _queryCts = new CancellationTokenSource();
        var tok = _queryCts.Token;
        var folder = _sessionFolderPath;
        var typing = _typing;
        var maxResults = _settings.ExplorerEverythingQuickFindMaxResults;
        var spec = SearchQuerySpec.Parse(typing);
        var typingTrim = typing.Trim();
        var hasTyping = !spec.IsEmpty;

        // 早期 debounce 写到 120ms，叠加上 LL 钩 → BeginInvoke 的自然延迟，每次按键都要等
        // 100ms+ 才出结果，连按时表现为「打了字面板像没反应」。
        // Everything IPC 单次查询通常 <5ms，把节流压到 30ms 已足够合并连按，又不会感知卡顿。
        _ = Task.Run(() =>
        {
            if (tok.WaitHandle.WaitOne(30)) return;
            if (gen != _queryGen) return;

            void PostUi(List<QuickFindResultItem> items, string? status, string? countLine = null)
            {
                if (tok.IsCancellationRequested || gen != _queryGen) return;
                var hl = hasTyping ? typingTrim : null;
                _dispatcher.BeginInvoke(() =>
                {
                    if (gen != _queryGen || !_session || _window == null) return;
                    _window.SetResults(items, status, countLine, hl);
                    _window.SetQueryText(_sessionFolderDisplay, _typing, hl);
                }, DispatcherPriority.Background);
            }

            // ---------- 阶段 1：parent: 仅当前文件夹一层 + 关键词，立刻回显 ----------
            var parentSearch = BuildEverythingParentScopedSearch(folder, spec);
            var parentPaths = new List<string>();
            var okParent = false;
            var errParent = 0;
            EverythingIpc.InvokeExclusive(() =>
            {
                okParent = EverythingIpc.TryQueryFullPathsCore(parentSearch, maxResults, parentPaths, out errParent);
            });
            FilterQuickFindPaths(parentPaths, folder, spec);

            if (tok.IsCancellationRequested || gen != _queryGen) return;

            if (!hasTyping)
            {
                if (!okParent)
                {
                    PostUi([], FormatError(errParent));
                    return;
                }

                var listOnly = QuickFindResultItem.FromFullPaths(parentPaths, folder);
                PostUi(listOnly, listOnly.Count == 0 ? "无匹配项" : null);
                return;
            }

            if (!okParent)
                PostUi([], $"{FormatError(errParent)} · 正在检索当前路径树下…");
            else
            {
                var parentOnly = QuickFindResultItem.FromFullPaths(parentPaths, folder);
                var hint1 = parentOnly.Count == 0
                    ? "当前文件夹无直接匹配 · 正在检索当前路径树下…"
                    : "正在检索当前路径树下…";
                var count1 = parentOnly.Count > 0 ? $"{parentOnly.Count} 项（当前文件夹）" : "";
                PostUi(parentOnly, hint1, string.IsNullOrEmpty(count1) ? null : count1);
            }

            if (tok.IsCancellationRequested || gen != _queryGen) return;

            // ---------- 阶段 2：path: 当前路径树下任意深度 + 关键词，合并后回显 ----------
            var pathSearch = BuildEverythingPathSubtreeScopedSearch(folder, spec);
            var pathPaths = new List<string>();
            var okPath = false;
            var errPath = 0;
            EverythingIpc.InvokeExclusive(() =>
            {
                okPath = EverythingIpc.TryQueryFullPathsCore(pathSearch, maxResults, pathPaths, out errPath);
            });
            FilterQuickFindPaths(pathPaths, folder, spec);

            if (tok.IsCancellationRequested || gen != _queryGen) return;

            var localMerged = MergePathListsPreferFirst(parentPaths, pathPaths, maxResults);
            var okLocal = okParent || okPath;
            var localItems = okLocal
                ? QuickFindResultItem.FromFullPaths(localMerged, folder)
                : [];

            if (!okLocal)
            {
                PostUi([], $"{FormatError(errParent)} · {FormatError(errPath)} · 正在检索全盘…");
            }
            else
            {
                var hint2 = localItems.Count == 0
                    ? "当前路径树下无匹配 · 正在检索全盘…"
                    : "正在补充全盘结果…";
                var count2 = localItems.Count > 0 ? $"{localItems.Count} 项（当前路径）" : "";
                PostUi(localItems, hint2, string.IsNullOrEmpty(count2) ? null : count2);
            }

            if (tok.IsCancellationRequested || gen != _queryGen) return;

            // ---------- 阶段 3：全盘关键词，与本地合并 ----------
            var globalPaths = new List<string>();
            var okGlobal = false;
            var errGlobal = 0;
            EverythingIpc.InvokeExclusive(() =>
            {
                okGlobal = EverythingIpc.TryQueryFullPathsCore(BuildEverythingBroadSearch(spec), maxResults, globalPaths, out errGlobal);
            });
            FilterQuickFindPaths(globalPaths, folder, spec);

            if (tok.IsCancellationRequested || gen != _queryGen) return;

            List<QuickFindResultItem> items;
            string? status = null;
            string? countLine = null;

            if (!okLocal && !okGlobal)
            {
                items = [];
                status = $"parent: {FormatError(errParent)} · path: {FormatError(errPath)}";
            }
            else if (!okLocal && okGlobal)
            {
                items = QuickFindResultItem.FromScopedAndGlobalLists(Array.Empty<string>(), globalPaths, folder, maxResults);
                status = "当前路径检索失败，仅显示全盘";
                countLine = items.Count > 0 ? $"{items.Count} 项（全盘）" : "";
            }
            else if (okLocal && !okGlobal)
            {
                items = QuickFindResultItem.FromFullPaths(localMerged, folder);
                if (items.Count == 0)
                    status = "无匹配项";
                else
                    status = $"全盘检索不可用（{FormatError(errGlobal)}）";
            }
            else
            {
                items = QuickFindResultItem.FromScopedAndGlobalLists(localMerged, globalPaths, folder, maxResults);
                if (items.Count == 0)
                    status = "无匹配项";
                else
                {
                    var nGlob = items.Count(x => x.IsGlobalMatch);
                    var nScoped = items.Count - nGlob;
                    countLine = nGlob > 0
                        ? $"共 {items.Count} 项（当前路径 {nScoped} · 全盘 {nGlob}）"
                        : $"{items.Count} 项";
                }
            }

            PostUi(items, status, countLine);
        }, tok);
    }
}

