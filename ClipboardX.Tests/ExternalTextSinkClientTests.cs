using System.Text.Json;
using ClipboardManager;

namespace ClipboardX.Tests;

public sealed class ExternalTextSinkClientTests
{
    [Fact]
    public void PipeName_IsVersionedAndScopedToWindowsSession()
    {
        Assert.Equal(
            "komorebi-shortcuts-tauri.external-text-sink.v1.session-42",
            ExternalTextSinkClient.PipeNameForSession(42));
    }

    [Fact]
    public void BeginRequest_UsesSharedWireFieldNames()
    {
        var json = ExternalTextSinkClient.SerializeBeginRequest("ClipboardX");
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        Assert.Equal("begin_overlay", root.GetProperty("type").GetString());
        Assert.Equal(1, root.GetProperty("protocolVersion").GetInt32());
        Assert.Equal("ClipboardX", root.GetProperty("source").GetString());
        Assert.Equal("insert_text", root.GetProperty("capabilities")[0].GetString());
    }

    [Fact]
    public void Response_ParsesLauncherLease()
    {
        var response = ExternalTextSinkClient.DeserializeResponse(
            """{"protocolVersion":1,"accepted":true,"leaseId":"abc","launcherSessionId":7,"expiresAtMs":9}""");
        Assert.NotNull(response);
        Assert.True(response.Accepted);
        Assert.Equal("abc", response.LeaseId);
        Assert.Equal(7, response.LauncherSessionId);
        Assert.Equal(9, response.ExpiresAtMs);
    }
}
