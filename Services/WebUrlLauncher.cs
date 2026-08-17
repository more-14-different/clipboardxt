using System.Diagnostics;
using System.IO;
using ClipboardManager.Models;

namespace ClipboardManager;

internal static class WebUrlLauncher
{
    internal static string MetadataLabel => UiLanguage.IsEnglish ? "URL" : "网址";
    internal const string MetadataSearchText = "网址 URL";

    private static readonly HashSet<string> KnownBrowserExecutables = new(
        [
            "chrome.exe",
            "msedge.exe",
            "firefox.exe",
            "brave.exe",
            "vivaldi.exe",
            "opera.exe",
            "opera_gx.exe",
            "chromium.exe",
            "arc.exe",
            "waterfox.exe",
            "librewolf.exe",
            "floorp.exe",
            "zen.exe",
            "thorium.exe",
            "duckduckgo.exe",
            "yandex.exe",
            "360chrome.exe",
            "360se.exe",
            "qqbrowser.exe",
            "sogouexplorer.exe",
            "maxthon.exe",
            "avastbrowser.exe",
            "ccleanerbrowser.exe",
            "avgbrowser.exe",
            "iexplore.exe",
            "microsoftedge.exe",
            "browser.exe",
            "palemoon.exe",
            "seamonkey.exe",
            "basilisk.exe",
            "slimjet.exe",
            "centbrowser.exe",
            "catsxp.exe",
            "twinkstar.exe",
            "2345explorer.exe",
            "liebao.exe",
            "ucbrowser.exe",
            "quark.exe",
            "quarkpc.exe",
            "dragon.exe",
            "iron.exe",
            "epic.exe",
            "sidekick.exe",
            "wavebox.exe",
            "coccoc.exe",
            "naver_whale.exe",
            "ghostbrowser.exe",
        ],
        StringComparer.OrdinalIgnoreCase);

    internal readonly record struct Candidate(string? Text, ClipboardSourceInfo? Source);

    internal readonly record struct OpenRequest(Uri Uri, string? BrowserExecutable);

    internal enum LaunchRoute
    {
        SourceBrowser,
        DefaultBrowser,
        DefaultBrowserFallback,
        Failed,
    }

    internal readonly record struct LaunchResult(
        LaunchRoute Route,
        Exception? SourceBrowserError = null,
        Exception? DefaultBrowserError = null)
    {
        internal bool Success => Route != LaunchRoute.Failed;
    }

    internal static IReadOnlyList<OpenRequest> BuildRequests(IEnumerable<Candidate> candidates)
    {
        var requests = new List<OpenRequest>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var candidate in candidates)
        {
            if (!TryNormalize(candidate.Text, out var uri))
                continue;

            var browserExecutable = TryResolveSourceBrowser(candidate.Source, out var executable)
                ? executable
                : null;
            var identity = $"{browserExecutable}\0{uri.AbsoluteUri}";
            if (seen.Add(identity))
                requests.Add(new OpenRequest(uri, browserExecutable));
        }

        return requests;
    }

    internal static bool TryNormalize(string? candidate, out Uri uri)
    {
        uri = null!;
        if (string.IsNullOrWhiteSpace(candidate))
            return false;

        var trimmed = candidate.Trim();
        if (trimmed.Any(char.IsWhiteSpace)
            || trimmed.Any(char.IsControl)
            || !Uri.IsWellFormedUriString(trimmed, UriKind.Absolute)
            || !Uri.TryCreate(trimmed, UriKind.Absolute, out var parsed)
            || (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps)
            || string.IsNullOrWhiteSpace(parsed.Host)
            || parsed.HostNameType == UriHostNameType.Unknown)
        {
            return false;
        }

        uri = parsed;
        return true;
    }

    internal static bool IsMetadataSearchToken(string token, string? pinyinMode = null) =>
        token.Equals("网址", StringComparison.OrdinalIgnoreCase)
        || token.Equals("URL", StringComparison.OrdinalIgnoreCase)
        || PinyinSearchIndex.MatchesToken("网址", token, pinyinMode);

    internal static bool TryResolveSourceBrowser(
        ClipboardSourceInfo? source,
        out string executable)
    {
        executable = "";
        if (source == null)
            return false;

        if (TryResolveKnownBrowserName(source.ExePath, pathCandidate: true, out executable)
            || TryResolveKnownBrowserName(source.ExeName, pathCandidate: false, out executable)
            || TryResolveKnownBrowserName(source.AppName, pathCandidate: false, out executable))
        {
            return true;
        }

        return false;
    }

    internal static LaunchResult Open(
        OpenRequest request,
        Action<ProcessStartInfo>? start = null)
    {
        start ??= static startInfo => { Process.Start(startInfo); };
        Exception? sourceBrowserError = null;
        if (!string.IsNullOrWhiteSpace(request.BrowserExecutable))
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = request.BrowserExecutable,
                    UseShellExecute = true,
                };
                startInfo.ArgumentList.Add(request.Uri.AbsoluteUri);
                start(startInfo);
                return new LaunchResult(LaunchRoute.SourceBrowser);
            }
            catch (Exception ex)
            {
                sourceBrowserError = ex;
            }
        }

        try
        {
            start(new ProcessStartInfo
            {
                FileName = request.Uri.AbsoluteUri,
                UseShellExecute = true,
            });
            return new LaunchResult(
                sourceBrowserError == null
                    ? LaunchRoute.DefaultBrowser
                    : LaunchRoute.DefaultBrowserFallback,
                SourceBrowserError: sourceBrowserError);
        }
        catch (Exception ex)
        {
            return new LaunchResult(
                LaunchRoute.Failed,
                SourceBrowserError: sourceBrowserError,
                DefaultBrowserError: ex);
        }
    }

    private static bool TryResolveKnownBrowserName(
        string? candidate,
        bool pathCandidate,
        out string executable)
    {
        executable = "";
        if (string.IsNullOrWhiteSpace(candidate))
            return false;

        var trimmed = candidate.Trim().Trim('"');
        string fileName;
        try
        {
            fileName = Path.GetFileName(trimmed);
        }
        catch
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(Path.GetExtension(fileName)))
            fileName += ".exe";
        if (!KnownBrowserExecutables.Contains(fileName))
            return false;

        if (pathCandidate && File.Exists(trimmed))
        {
            try
            {
                executable = Path.GetFullPath(trimmed);
                return true;
            }
            catch
            {
                // Fall through to the executable name so Windows App Paths can resolve it.
            }
        }

        executable = fileName;
        return true;
    }
}
