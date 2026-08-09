using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Threading;

namespace ClipboardManager;

/// <summary>
/// 与原生 ShellNavigate DLL 共用路径规则：<see cref="Environment.SpecialFolder.LocalApplicationData"/>下的 ClipboardX\shell_navigate.log
/// </summary>
internal static class ShellNavigateLog
{
    private static readonly ConcurrentQueue<string> Queue = new();
    private static readonly AutoResetEvent Signal = new(false);
    private static readonly ManualResetEventSlim Drained = new(true);
    private static readonly int MaxBytesBeforeTrim = 2_000_000;
    private static int _started;

    public static string LogFilePath => AppPaths.ShellNavigateLogFile;

    public static void Write(string source, string message)
    {
        try
        {
            var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{source}] {message}{Environment.NewLine}";
            Drained.Reset();
            Queue.Enqueue(line);
            EnsureWriterStarted();
            Signal.Set();
        }
        catch
        {
            /* 日志本身不得影响主流程 */
        }
    }

    public static void WriteInjector(string message) => Write("inject", message);

    public static void WriteInjectorWin32(string message, int? lastError = null)
    {
        if (lastError.HasValue)
            Write("inject", $"{message} (Win32={lastError.Value})");
        else
            Write("inject", message);
    }

    public static void FlushPending(int timeoutMs = 1000)
    {
        if (Queue.IsEmpty && Drained.IsSet) return;
        EnsureWriterStarted();
        Signal.Set();
        var deadline = Environment.TickCount64 + Math.Max(1, timeoutMs);
        while (!Queue.IsEmpty || !Drained.IsSet)
        {
            var remaining = deadline - Environment.TickCount64;
            if (remaining <= 0) return;
            Drained.Wait((int)Math.Min(remaining, int.MaxValue));
            if (!Queue.IsEmpty)
            {
                Drained.Reset();
                Signal.Set();
            }
        }
    }

    private static void EnsureWriterStarted()
    {
        if (Interlocked.CompareExchange(ref _started, 1, 0) != 0) return;
        var thread = new Thread(WriterLoop)
        {
            IsBackground = true,
            Name = "ClipboardX-ShellNavigateLogWriter",
            Priority = ThreadPriority.BelowNormal,
        };
        thread.Start();
    }

    private static void WriterLoop()
    {
        var buffer = new StringBuilder(4096);
        while (true)
        {
            Signal.WaitOne();
            do
            {
                buffer.Clear();
                while (Queue.TryDequeue(out var line))
                    buffer.Append(line);

                if (buffer.Length > 0)
                    AppendBatch(buffer.ToString());
            }
            while (!Queue.IsEmpty);

            Drained.Set();
            if (!Queue.IsEmpty)
            {
                Drained.Reset();
                Signal.Set();
            }
        }
    }

    private static void AppendBatch(string text)
    {
        try
        {
            var dir = Path.GetDirectoryName(LogFilePath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            File.AppendAllText(LogFilePath, text, Encoding.UTF8);
            TrimIfHuge();
        }
        catch
        {
            /* ignore */
        }
    }

    private static void TrimIfHuge()
    {
        try
        {
            var fi = new FileInfo(LogFilePath);
            if (!fi.Exists || fi.Length <= MaxBytesBeforeTrim) return;
            // 保留尾部约一半，避免无限增长
            using var fs = new FileStream(LogFilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
            var keep = (int)Math.Min(MaxBytesBeforeTrim / 2, fi.Length);
            var buf = new byte[keep];
            fs.Seek(-keep, SeekOrigin.End);
            fs.ReadExactly(buf, 0, keep);
            fs.SetLength(0);
            fs.Write(buf, 0, keep);
        }
        catch
        {
            /* ignore */
        }
    }
}
