using System.Diagnostics;

namespace ClipboardManager;

internal static class WebUrlLauncher
{
    internal static IReadOnlyList<Uri> CollectUnique(IEnumerable<string?> candidates)
    {
        var urls = new List<Uri>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var candidate in candidates)
        {
            if (TryNormalize(candidate, out var uri) && seen.Add(uri.AbsoluteUri))
                urls.Add(uri);
        }

        return urls;
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

    internal static void Open(Uri uri)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = uri.AbsoluteUri,
            UseShellExecute = true,
        });
    }
}
