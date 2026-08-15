using ClipboardManager;
using ClipboardManager.Models;

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
    public void BuildRequests_FiltersInvalidValuesAndPreservesFirstListOrder()
    {
        var requests = WebUrlLauncher.BuildRequests([
            new("https://second.example/path", null),
            new("not a URL", null),
            new("https://FIRST.example/", null),
            new("HTTPS://first.example/", null),
            new(null, null),
            new("file:///C:/Windows/notepad.exe", null),
        ]);

        Assert.Equal(
            ["https://second.example/path", "https://first.example/"],
            requests.Select(request => request.Uri.AbsoluteUri));
    }

    [Theory]
    [InlineData("chrome.exe")]
    [InlineData("msedge.exe")]
    [InlineData("firefox.exe")]
    [InlineData("brave.exe")]
    [InlineData("vivaldi.exe")]
    [InlineData("opera.exe")]
    public void TryResolveSourceBrowser_RecognizesKnownBrowserExecutableNames(string exeName)
    {
        var source = new ClipboardSourceInfo { ExeName = exeName };

        Assert.True(WebUrlLauncher.TryResolveSourceBrowser(source, out var executable));
        Assert.Equal(exeName, executable, ignoreCase: true);
    }

    [Fact]
    public void TryResolveSourceBrowser_DoesNotTreatChromiumWindowClassAsBrowser()
    {
        var source = new ClipboardSourceInfo
        {
            ExeName = "Code.exe",
            WindowClass = "Chrome_WidgetWin_1",
        };

        Assert.False(WebUrlLauncher.TryResolveSourceBrowser(source, out _));
    }

    [Fact]
    public void BuildRequests_DeduplicatesPerDestinationBrowser()
    {
        var chrome = new ClipboardSourceInfo { ExeName = "chrome.exe" };
        var firefox = new ClipboardSourceInfo { ExeName = "firefox.exe" };

        var requests = WebUrlLauncher.BuildRequests([
            new("https://example.com", chrome),
            new("HTTPS://EXAMPLE.COM/", chrome),
            new("https://example.com", firefox),
            new("https://default.example", new ClipboardSourceInfo { ExeName = "Code.exe" }),
        ]);

        Assert.Collection(
            requests,
            request => Assert.Equal("chrome.exe", request.BrowserExecutable),
            request => Assert.Equal("firefox.exe", request.BrowserExecutable),
            request => Assert.Null(request.BrowserExecutable));
    }

    [Fact]
    public void Open_SourceBrowserFailureFallsBackToDefaultBrowser()
    {
        var attempts = new List<string>();
        var uri = new Uri("https://example.com/");

        var result = WebUrlLauncher.Open(
            new WebUrlLauncher.OpenRequest(uri, "chrome.exe"),
            startInfo =>
            {
                attempts.Add(startInfo.FileName);
                if (startInfo.FileName == "chrome.exe")
                    throw new InvalidOperationException("browser unavailable");
            });

        Assert.Equal(WebUrlLauncher.LaunchRoute.DefaultBrowserFallback, result.Route);
        Assert.IsType<InvalidOperationException>(result.SourceBrowserError);
        Assert.Equal(["chrome.exe", uri.AbsoluteUri], attempts);
    }
}
