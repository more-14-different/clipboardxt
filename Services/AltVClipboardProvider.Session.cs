using System.Diagnostics;
using System.IO;
using System.Text;

namespace ClipboardManager;

internal static partial class AltVClipboardProvider
{
    internal sealed class Session : IAsyncDisposable
    {
        private readonly Process? _process;
        private readonly string _requestFile;
        private readonly string _resultFile;
        private readonly string _stopFile;
        private bool _disposed;

        public Session(Result result, Process? process, string requestFile, string resultFile, string stopFile)
        {
            Result = result;
            _process = process;
            _requestFile = requestFile;
            _resultFile = resultFile;
            _stopFile = stopFile;
        }

        public Result Result { get; }

        public async ValueTask DisposeAsync()
        {
            if (_disposed)
                return;

            _disposed = true;
            try
            {
                if (_process is { HasExited: false })
                {
                    TryTouch(_stopFile);
                    using var cts = new CancellationTokenSource(1500);
                    try
                    {
                        await _process.WaitForExitAsync(cts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        try { _process.Kill(entireProcessTree: true); } catch { /* ignore */ }
                    }
                }
            }
            finally
            {
                _process?.Dispose();
                TryDelete(_requestFile);
                TryDelete(_resultFile);
                TryDelete(_stopFile);
            }
        }
    }

    public static async Task<Session> StartTextSessionAsync(string text)
    {
        var directory = Path.Combine(Path.GetTempPath(), "ClipboardX", "altv-provider");
        Directory.CreateDirectory(directory);

        var token = Guid.NewGuid().ToString("N");
        var requestFile = Path.Combine(directory, $"request-{token}.txt");
        var resultFile = Path.Combine(directory, $"result-{token}.json");
        var stopFile = Path.Combine(directory, $"stop-{token}.signal");
        await File.WriteAllTextAsync(requestFile, text, new UTF8Encoding(false));

        var executablePath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
        {
            return new Session(
                new Result(false, false, "ProcessPath unavailable"),
                null,
                requestFile,
                resultFile,
                stopFile);
        }

        var processStartInfo = new ProcessStartInfo(executablePath)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
        };
        processStartInfo.ArgumentList.Add(ModeArg);
        processStartInfo.ArgumentList.Add(RequestArg);
        processStartInfo.ArgumentList.Add(requestFile);
        processStartInfo.ArgumentList.Add(ResultArg);
        processStartInfo.ArgumentList.Add(resultFile);
        processStartInfo.ArgumentList.Add(StopArg);
        processStartInfo.ArgumentList.Add(stopFile);

        var process = Process.Start(processStartInfo);
        if (process == null)
        {
            return new Session(
                new Result(false, false, "Process.Start returned null"),
                null,
                requestFile,
                resultFile,
                stopFile);
        }

        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.ElapsedMilliseconds < ProviderResultTimeoutMs)
        {
            if (File.Exists(resultFile))
            {
                var result = DeserializeResult(await File.ReadAllTextAsync(resultFile, Encoding.UTF8));
                if (result == null)
                {
                    return new Session(
                        new Result(false, false, "Provider result parse failed"),
                        process,
                        requestFile,
                        resultFile,
                        stopFile);
                }

                return new Session(result.Value, process, requestFile, resultFile, stopFile);
            }

            if (process.HasExited)
                break;

            await Task.Delay(25);
        }

        return new Session(
            new Result(
                false,
                false,
                process.HasExited
                    ? $"Provider exited ({process.ExitCode}) without result"
                    : "Provider result timeout"),
            process,
            requestFile,
            resultFile,
            stopFile);
    }
}
