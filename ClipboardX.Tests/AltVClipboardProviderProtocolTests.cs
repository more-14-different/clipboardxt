using ClipboardManager;

namespace ClipboardX.Tests;

public sealed class AltVClipboardProviderProtocolTests
{
    [Theory]
    [InlineData("--altv-provider-settext")]
    [InlineData("--ALTV-PROVIDER-SETTEXT")]
    public void IsProviderCommand_RequiresModeAsFirstArgument(string mode)
    {
        Assert.True(AltVClipboardProvider.IsProviderCommand([mode, "--other"]));
        Assert.False(AltVClipboardProvider.IsProviderCommand(["--other", mode]));
        Assert.False(AltVClipboardProvider.IsProviderCommand([]));
    }

    [Fact]
    public void TryParseProviderArguments_ParsesCaseInsensitiveNamedFiles()
    {
        var parsed = AltVClipboardProvider.TryParseProviderArguments(
            [
                "--altv-provider-settext",
                "--STOP-FILE", "stop.signal",
                "--request-file", "request.txt",
                "--RESULT-FILE", "result.json",
            ],
            out var arguments);

        Assert.True(parsed);
        Assert.Equal("request.txt", arguments.RequestFile);
        Assert.Equal("result.json", arguments.ResultFile);
        Assert.Equal("stop.signal", arguments.StopFile);
    }

    [Theory]
    [InlineData("--request-file")]
    [InlineData("--result-file")]
    [InlineData("--stop-file")]
    public void TryParseProviderArguments_RejectsMissingOrBlankFile(string missingArgument)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["--request-file"] = "request.txt",
            ["--result-file"] = "result.json",
            ["--stop-file"] = "stop.signal",
        };
        values[missingArgument] = " ";

        var parsed = AltVClipboardProvider.TryParseProviderArguments(
            [
                "--altv-provider-settext",
                "--request-file", values["--request-file"],
                "--result-file", values["--result-file"],
                "--stop-file", values["--stop-file"],
            ],
            out _);

        Assert.False(parsed);
    }

    [Theory]
    [InlineData(true, false, "")]
    [InlineData(false, true, "clipboard locked")]
    public void ResultJson_RoundTrips(bool success, bool clipboardLocked, string error)
    {
        var expected = new AltVClipboardProvider.Result(success, clipboardLocked, error);

        var actual = AltVClipboardProvider.DeserializeResult(
            AltVClipboardProvider.SerializeResult(expected));

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void DeserializeResult_ReturnsNullForJsonNull()
    {
        Assert.Null(AltVClipboardProvider.DeserializeResult("null"));
    }
}
