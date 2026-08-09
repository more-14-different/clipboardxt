using System.IO;

namespace ClipboardManager;

/// <summary>
/// 通过 Everything 随附的原生 DLL 做同步 IPC 查询（需本机已运行 Everything）。
/// 同一进程内 Everything IPC 使用全局状态，所有查询必须串行执行。
/// </summary>
internal static partial class EverythingIpc
{
    private static readonly object IpcSync = new();

    /// <summary>未在任何候选路径找到 DLL（与 Everything 进程是否运行无关）。</summary>
    public const int LastErrorDllNotFound = -100;

    /// <summary>P/Invoke 调用异常（位宽/入口不匹配等）。</summary>
    public const int LastErrorInterop = -101;

    /// <summary>在已持有 Everything IPC 全局锁时执行一组查询。</summary>
    public static void InvokeExclusive(Action action)
    {
        lock (IpcSync) action();
    }

    /// <summary>尝试查询；失败时 <paramref name="lastError"/> 为 Everything_GetLastError，DLL 未加载为 <see cref="LastErrorDllNotFound"/> 等。</summary>
    public static bool TryQueryFullPaths(string searchExpression, int maxResults, List<string> paths, out int lastError)
    {
        lock (IpcSync)
            return TryQueryFullPathsCore(searchExpression, maxResults, paths, out lastError);
    }

    /// <summary>限定为文件夹的检索，并剔除已不存在或不符合精确搜索语义的路径。</summary>
    public static bool TryQueryFolderPaths(string userTyping, int maxResults, List<string> paths, out int lastError)
    {
        paths.Clear();
        lastError = LastErrorDllNotFound;
        var spec = SearchQuerySpec.Parse(userTyping);
        if (spec.IsEmpty)
        {
            lastError = unchecked((int)EverythingErrorOk);
            return true;
        }

        var query = EverythingFolderQueryBuilder.Build(spec);
        var queryMax = EverythingFolderQueryBuilder.CandidateQueryMax(maxResults);

        lock (IpcSync)
        {
            var candidates = new List<string>();
            if (!TryQueryFullPathsCore(query, queryMax, candidates, out lastError, matchPath: true))
                return false;

            foreach (var path in candidates)
            {
                try
                {
                    if (Directory.Exists(path) && spec.MatchesText(FileJumpPickerRow.NormalizeDirectoryAnchorPath(path)))
                        paths.Add(path);
                }
                catch
                {
                    /* ignore bad path */
                }

                if (paths.Count >= maxResults)
                    break;
            }

            lastError = unchecked((int)EverythingErrorOk);
            return true;
        }
    }
}
