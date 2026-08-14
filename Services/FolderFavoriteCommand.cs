using System.IO;
using System.IO.Pipes;

namespace ClipboardManager;

internal static class FolderFavoriteCommand
{
    internal const string AddArgument = "--add-folder-favorite";
    private const string AddedResponse = "added";
    private const string ExistingResponse = "exists";
    private const string InvalidResponse = "invalid";

    internal static string PipeName => AppPaths.MutexName + "_FolderFavorite_v1";

    internal static bool IsRequested(IReadOnlyList<string> args) =>
        args.Any(arg => string.Equals(arg, AddArgument, StringComparison.OrdinalIgnoreCase));

    internal static bool TryParse(IReadOnlyList<string> args, out string path)
    {
        path = "";
        for (var i = 0; i < args.Count; i++)
        {
            if (!string.Equals(args[i], AddArgument, StringComparison.OrdinalIgnoreCase))
                continue;
            if (i + 1 >= args.Count || string.IsNullOrWhiteSpace(args[i + 1]))
                return false;
            return TryNormalizeExistingDirectory(args[i + 1], out path);
        }
        return false;
    }

    internal static bool TryAdd(AppSettings settings, string requestedPath, out string response)
    {
        response = InvalidResponse;
        if (!TryNormalizeExistingDirectory(requestedPath, out var path))
            return false;

        if (settings.FolderFavorites.Any(f => PathsEqual(f.Path, path)))
        {
            response = ExistingResponse;
            return true;
        }

        settings.FolderFavorites.Add(new FolderFavoriteEntry
        {
            Phrase = GuessPhrase(path),
            Path = path,
        });
        settings.SaveSync();
        response = AddedResponse;
        return true;
    }

    internal static bool TrySendToRunningInstance(string path, int timeoutMilliseconds = 900)
    {
        try
        {
            using var timeout = new CancellationTokenSource(timeoutMilliseconds);
            using var client = new NamedPipeClientStream(
                ".", PipeName, PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            client.ConnectAsync(timeoutMilliseconds, timeout.Token).GetAwaiter().GetResult();
            using var writer = new StreamWriter(client, leaveOpen: true) { AutoFlush = true };
            using var reader = new StreamReader(client, leaveOpen: true);
            writer.WriteLine(path);
            var response = reader.ReadLineAsync(timeout.Token).AsTask().GetAwaiter().GetResult();
            return response is AddedResponse or ExistingResponse;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryNormalizeExistingDirectory(string rawPath, out string path)
    {
        path = "";
        try
        {
            var full = Path.GetFullPath(rawPath.Trim().Trim('"'));
            if (!Directory.Exists(full)) return false;

            var root = Path.GetPathRoot(full);
            path = !string.IsNullOrEmpty(root) && PathsEqualWithoutNormalization(full, root)
                ? root
                : full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool PathsEqual(string? left, string right)
    {
        if (string.IsNullOrWhiteSpace(left)) return false;
        try
        {
            var normalizedLeft = Path.GetFullPath(left.Trim());
            return PathsEqualWithoutNormalization(normalizedLeft, right);
        }
        catch
        {
            return false;
        }
    }

    private static bool PathsEqualWithoutNormalization(string left, string right) =>
        string.Equals(
            left.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            right.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            StringComparison.OrdinalIgnoreCase);

    private static string GuessPhrase(string path)
    {
        try
        {
            var phrase = Path.GetFileName(path.TrimEnd(
                Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            return string.IsNullOrWhiteSpace(phrase) ? path : phrase;
        }
        catch
        {
            return "收藏";
        }
    }
}

internal sealed class FolderFavoriteCommandServer : IDisposable
{
    private readonly Func<string, string> _handler;
    private readonly CancellationTokenSource _stop = new();
    private readonly Task _listenTask;

    internal FolderFavoriteCommandServer(Func<string, string> handler)
    {
        _handler = handler;
        _listenTask = Task.Run(ListenAsync);
    }

    private async Task ListenAsync()
    {
        while (!_stop.IsCancellationRequested)
        {
            try
            {
                await using var server = new NamedPipeServerStream(
                    FolderFavoriteCommand.PipeName,
                    PipeDirection.InOut,
                    1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await server.WaitForConnectionAsync(_stop.Token).ConfigureAwait(false);
                using var reader = new StreamReader(server, leaveOpen: true);
                await using var writer = new StreamWriter(server, leaveOpen: true) { AutoFlush = true };
                var path = await reader.ReadLineAsync(_stop.Token).ConfigureAwait(false);
                var response = string.IsNullOrWhiteSpace(path) ? "invalid" : _handler(path);
                await writer.WriteLineAsync(response).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (_stop.IsCancellationRequested)
            {
                break;
            }
            catch
            {
                if (_stop.IsCancellationRequested) break;
                await Task.Delay(80).ConfigureAwait(false);
            }
        }
    }

    public void Dispose()
    {
        _stop.Cancel();
        try { _listenTask.Wait(300); } catch { }
        _stop.Dispose();
    }
}
