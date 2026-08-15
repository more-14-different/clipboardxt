using ClipboardManager;

namespace ClipboardX.Tests;

public sealed class WebUrlLauncherTests
{
    [Theory]
    [InlineData("https://example.com")]
    [InlineData("http://example.com:8080/path?value=1#result")]
    [InlineData("  https://example.com/path  ")]
    [InlineData("http://localhost:5000/")]
    [InlineData("https://127.0.0.1/")]
    [InlineData("https://[::1]/")]
    public void TryNormalize_AcceptsAbsoluteHttpAndHttpsUrls(string candidate)
    {
        Assert.True(WebUrlLauncher.TryNormalize(candidate, out var uri));
        Assert.True(uri.Scheme is "http" or "https");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("example.com")]
    [InlineData("www.example.com")]
    [InlineData("https://")]
    [InlineData("https://example.com/path with space")]
    [InlineData("https://example.com\nhttps://second.example")]
    [InlineData("ftp://example.com/file")]
    [InlineData("file:///C:/Windows/System32/notepad.exe")]
    [InlineData("javascript:alert(1)")]
    [InlineData("mailto:user@example.com")]
    public void TryNormalize_RejectsNonWebOrMalformedValues(string? candidate)
    {
        Assert.False(WebUrlLauncher.TryNormalize(candidate, out _));
    }

    [Fact]
    public void CollectUnique_FiltersInvalidValuesAndPreservesFirstListOrder()
    {
        var urls = WebUrlLauncher.CollectUnique([
            "https://second.example/path",
            "not a URL",
            "https://FIRST.example/",
            "HTTPS://first.example/",
            null,
            "file:///C:/Windows/notepad.exe",
        ]);

        Assert.Equal(
            ["https://second.example/path", "https://first.example/"],
            urls.Select(uri => uri.AbsoluteUri));
    }
}
