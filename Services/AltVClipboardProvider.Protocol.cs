using System.IO;
using System.Text;
using System.Text.Json;

namespace ClipboardManager;

internal static partial class AltVClipboardProvider
{
    internal readonly record struct ProviderArguments(
        string RequestFile,
        string ResultFile,
        string StopFile);

    private sealed class Payload
    {
        public bool Success { get; set; }
        public bool ClipboardLocked { get; set; }
        public string Error { get; set; } = "";
    }

    internal static bool IsProviderCommand(IReadOnlyList<string> args) =>
        args.Count > 0
        && string.Equals(args[0], ModeArg, StringComparison.OrdinalIgnoreCase);

    internal static bool TryParseProviderArguments(
        IReadOnlyList<string> args,
        out ProviderArguments providerArguments)
    {
        providerArguments = default;
        if (!IsProviderCommand(args)) return false;

        var requestFile = GetArgValue(args, RequestArg);
        var resultFile = GetArgValue(args, ResultArg);
        var stopFile = GetArgValue(args, StopArg);
        if (string.IsNullOrWhiteSpace(requestFile)
            || string.IsNullOrWhiteSpace(resultFile)
            || string.IsNullOrWhiteSpace(stopFile))
        {
            return false;
        }

        providerArguments = new ProviderArguments(requestFile, resultFile, stopFile);
        return true;
    }

    internal static string SerializeResult(Result result) =>
        JsonSerializer.Serialize(new Payload
        {
            Success = result.Success,
            ClipboardLocked = result.ClipboardLocked,
            Error = result.Error,
        });

    internal static Result? DeserializeResult(string json)
    {
        var payload = JsonSerializer.Deserialize<Payload>(json);
        return payload == null
            ? null
            : new Result(payload.Success, payload.ClipboardLocked, payload.Error ?? "");
    }

    private static string? GetArgValue(IReadOnlyList<string> args, string name)
    {
        for (var i = 0; i < args.Count - 1; i++)
        {
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                return args[i + 1];
        }

        return null;
    }

    private static void WriteResult(string path, Result result)
    {
        File.WriteAllText(path, SerializeResult(result), new UTF8Encoding(false));
    }

    private static void TryTouch(string path)
    {
        try
        {
            File.WriteAllText(path, "stop", new UTF8Encoding(false));
        }
        catch
        {
            /* ignore */
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            /* ignore */
        }
    }
}
