using System.Runtime.InteropServices;
using System.Text;

namespace ClipboardManager;

internal static partial class EverythingIpc
{
    private const uint EverythingRequestFullPathAndFileName = 0x00000004;
    private const uint EverythingErrorOk = 0;
    private const int SearchFragmentCapacity = 2048;

    /// <summary>无锁；须在 <see cref="InvokeExclusive"/> 或 <see cref="TryQueryFullPaths"/> 已持锁时调用。</summary>
    internal static bool TryQueryFullPathsCore(
        string searchExpression,
        int maxResults,
        List<string> paths,
        out int lastError,
        bool matchPath = false)
    {
        paths.Clear();
        lastError = LastErrorDllNotFound;

        try
        {
            Everything_Reset();
        }
        catch (DllNotFoundException)
        {
            lastError = LastErrorDllNotFound;
            return false;
        }
        catch (EntryPointNotFoundException)
        {
            lastError = LastErrorInterop;
            return false;
        }

        try
        {
            // 不在此处依赖 Everything_IsDBLoaded：部分版本/安装下 IPC 已可查询但该标志仍长期为 0，会误报「未运行」。
            Everything_SetRequestFlags(EverythingRequestFullPathAndFileName);
            Everything_SetMax((uint)Math.Clamp(maxResults, 1, 10_000));
            Everything_SetMatchPath(matchPath ? 1 : 0);
            // ExplorerQuickFind 使用 parent: / path: 限定；文件夹补充搜索需要完整路径 AND，下游仍用 SearchQuerySpec 做锚定过滤。

            var buffer = searchExpression.AsSpan();
            if (buffer.Length > SearchFragmentCapacity)
                buffer = buffer[..SearchFragmentCapacity];
            Everything_SetSearchW(buffer.ToString());

            if (Everything_QueryW(1) == 0)
            {
                lastError = unchecked((int)Everything_GetLastError());
                return false;
            }

            var resultCount = Everything_GetNumResults();
            var cappedCount = Math.Min(resultCount, (uint)maxResults);
            for (uint i = 0; i < cappedCount; i++)
            {
                var pathBuffer = new StringBuilder(32768);
                Everything_GetResultFullPathNameW(i, pathBuffer, pathBuffer.Capacity);
                var path = pathBuffer.ToString().Trim();
                if (path.Length > 0)
                    paths.Add(path);
            }

            lastError = unchecked((int)EverythingErrorOk);
            return true;
        }
        catch (Exception)
        {
            lastError = LastErrorInterop;
            return false;
        }
    }

    [DllImport("Everything64.dll", CharSet = CharSet.Unicode)]
    private static extern void Everything_Reset();

    [DllImport("Everything64.dll", CharSet = CharSet.Unicode, EntryPoint = "Everything_SetSearchW")]
    private static extern void Everything_SetSearchW(string lpSearchString);

    [DllImport("Everything64.dll", CharSet = CharSet.Unicode)]
    private static extern void Everything_SetRequestFlags(uint dwRequestFlags);

    [DllImport("Everything64.dll", CharSet = CharSet.Unicode)]
    private static extern void Everything_SetMax(uint dwMax);

    [DllImport("Everything64.dll", CharSet = CharSet.Unicode)]
    private static extern void Everything_SetMatchPath(int bEnable);

    [DllImport("Everything64.dll", CharSet = CharSet.Unicode)]
    private static extern int Everything_QueryW(int bWait);

    [DllImport("Everything64.dll", CharSet = CharSet.Unicode)]
    private static extern uint Everything_GetNumResults();

    [DllImport("Everything64.dll", CharSet = CharSet.Unicode)]
    private static extern uint Everything_GetLastError();

    [DllImport("Everything64.dll", CharSet = CharSet.Unicode)]
    private static extern void Everything_GetResultFullPathNameW(uint nIndex, StringBuilder lpString, int nMaxCount);
}
