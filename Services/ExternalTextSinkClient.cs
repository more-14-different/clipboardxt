using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ClipboardManager;

internal static class ExternalTextSinkClient
{
    internal const int ProtocolVersion = 1;
    private const int ConnectTimeoutMs = 180;
    private const int OperationTimeoutMs = 500;

    internal readonly record struct Lease(
        string LeaseId,
        long LauncherSessionId,
        long ExpiresAtMs);

    private sealed class BeginRequest
    {
        [JsonPropertyName("type")]
        public string Type { get; init; } = "begin_overlay";

        [JsonPropertyName("protocolVersion")]
        public int ProtocolVersion { get; init; } = ExternalTextSinkClient.ProtocolVersion;

        [JsonPropertyName("source")]
        public string Source { get; init; } = "";

        [JsonPropertyName("capabilities")]
        public string[] Capabilities { get; init; } = ["insert_text"];
    }

    private sealed class InsertTextRequest
    {
        [JsonPropertyName("type")]
        public string Type { get; init; } = "insert_text";

        [JsonPropertyName("protocolVersion")]
        public int ProtocolVersion { get; init; } = ExternalTextSinkClient.ProtocolVersion;

        [JsonPropertyName("leaseId")]
        public string LeaseId { get; init; } = "";

        [JsonPropertyName("text")]
        public string Text { get; init; } = "";

        [JsonPropertyName("replaceSelection")]
        public bool ReplaceSelection { get; init; } = true;
    }

    private sealed class EndRequest
    {
        [JsonPropertyName("type")]
        public string Type { get; init; } = "end_overlay";

        [JsonPropertyName("protocolVersion")]
        public int ProtocolVersion { get; init; } = ExternalTextSinkClient.ProtocolVersion;

        [JsonPropertyName("leaseId")]
        public string LeaseId { get; init; } = "";
    }

    internal sealed class Response
    {
        [JsonPropertyName("protocolVersion")]
        public int ProtocolVersion { get; set; }

        [JsonPropertyName("accepted")]
        public bool Accepted { get; set; }

        [JsonPropertyName("leaseId")]
        public string? LeaseId { get; set; }

        [JsonPropertyName("launcherSessionId")]
        public long? LauncherSessionId { get; set; }

        [JsonPropertyName("expiresAtMs")]
        public long? ExpiresAtMs { get; set; }

        [JsonPropertyName("error")]
        public string? Error { get; set; }
    }

    internal static string PipeNameForSession(int sessionId) =>
        $"komorebi-shortcuts-tauri.external-text-sink.v{ProtocolVersion}.session-{sessionId}";

    internal static string SerializeBeginRequest(string source) =>
        JsonSerializer.Serialize(new BeginRequest { Source = source });

    internal static Response? DeserializeResponse(string json) =>
        JsonSerializer.Deserialize<Response>(json);

    public static async Task<Lease?> TryBeginOverlayAsync(
        string source,
        CancellationToken cancellationToken = default)
    {
        var response = await SendAsync(new BeginRequest { Source = source }, cancellationToken);
        if (response is not {
                Accepted: true,
                ProtocolVersion: ProtocolVersion,
                LeaseId: not null,
                LauncherSessionId: not null,
                ExpiresAtMs: not null
            })
        {
            return null;
        }

        return new Lease(
            response.LeaseId,
            response.LauncherSessionId.Value,
            response.ExpiresAtMs.Value);
    }

    public static async Task<bool> TryInsertTextAsync(
        Lease lease,
        string text,
        CancellationToken cancellationToken = default)
    {
        var response = await SendAsync(
            new InsertTextRequest { LeaseId = lease.LeaseId, Text = text },
            cancellationToken);
        return response is { Accepted: true, ProtocolVersion: ProtocolVersion };
    }

    public static async Task EndOverlayAsync(
        Lease lease,
        CancellationToken cancellationToken = default)
    {
        _ = await SendAsync(new EndRequest { LeaseId = lease.LeaseId }, cancellationToken);
    }

    private static async Task<Response?> SendAsync(
        object request,
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(OperationTimeoutMs);
        try
        {
            var sessionId = Process.GetCurrentProcess().SessionId;
            await using var pipe = new NamedPipeClientStream(
                ".",
                PipeNameForSession(sessionId),
                PipeDirection.InOut,
                PipeOptions.Asynchronous);
            await pipe.ConnectAsync(ConnectTimeoutMs, timeout.Token);

            var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
            await using var writer = new StreamWriter(pipe, encoding, leaveOpen: true)
            {
                AutoFlush = true,
                NewLine = "\n",
            };
            using var reader = new StreamReader(pipe, encoding, leaveOpen: true);
            await writer.WriteLineAsync(JsonSerializer.Serialize(request).AsMemory(), timeout.Token);
            var responseLine = await reader.ReadLineAsync(timeout.Token);
            return string.IsNullOrWhiteSpace(responseLine)
                ? null
                : DeserializeResponse(responseLine);
        }
        catch (Exception)
        {
            return null;
        }
    }
}
