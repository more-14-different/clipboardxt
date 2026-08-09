using System.IO;

namespace ClipboardManager;

internal static partial class GitHubUpdateService
{
    private const string SelfContainedSuffix = "win-x64-self-contained.zip";
    private const string NoRuntimeSuffix = "win-x64-no-runtime.zip";

    internal readonly struct ReleaseAssetInfo
    {
        public string Name { get; init; }
        public string DownloadUrl { get; init; }
        public long Size { get; init; }

        /// <summary>对应 CI 中的 no-runtime（框架依赖）zip；否则为 self-contained。</summary>
        public bool IsNoRuntimeVariant { get; init; }
    }

    internal readonly struct ReleaseAssetCandidate
    {
        public string Name { get; init; }
        public string DownloadUrl { get; init; }
        public long Size { get; init; }
    }

    internal readonly struct LatestReleaseInfo
    {
        public string TagName { get; init; }
        public string Body { get; init; }
        public ReleaseAssetInfo ChosenAsset { get; init; }
    }

    /// <summary>
    /// 当前进程是否从本机「dotnet\shared\Microsoft.NETCore.App」加载 CoreCLR（与 no-runtime / FDD 发行包一致）；
    /// 否则视为 self-contained，更新时优先下大包。
    /// </summary>
    internal static bool PreferNoRuntimeZip()
    {
        try
        {
            var location = typeof(object).Assembly.Location;
            if (string.IsNullOrWhiteSpace(location))
                return false;

            return location.Contains(
                       @"\dotnet\shared\Microsoft.NETCore.App\",
                       StringComparison.OrdinalIgnoreCase)
                   || location.Contains(
                       "/dotnet/shared/Microsoft.NETCore.App/",
                       StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static bool AssetMatchesProduct(string assetName, string primaryExecutableFileName)
    {
        if (string.IsNullOrEmpty(assetName)) return false;
        var productName = Path.GetFileNameWithoutExtension(primaryExecutableFileName);
        if (string.IsNullOrEmpty(productName)) productName = "ClipboardX";

        if (!assetName.StartsWith(productName + "-", StringComparison.OrdinalIgnoreCase))
            return false;

        if (string.Equals(productName, "ClipboardX", StringComparison.OrdinalIgnoreCase))
        {
            if (assetName.StartsWith("ClipboardX-clipboard-", StringComparison.OrdinalIgnoreCase))
                return false;
            if (assetName.StartsWith("ClipboardX-filejump-", StringComparison.OrdinalIgnoreCase))
                return false;
        }

        return true;
    }

    internal static ReleaseAssetInfo? PickZipAsset(
        IReadOnlyList<ReleaseAssetCandidate>? assets,
        bool preferNoRuntime,
        string primaryExecutableFileName)
    {
        if (assets == null || assets.Count == 0) return null;

        ReleaseAssetInfo? FirstMatching(string suffix, bool isNoRuntimeVariant)
        {
            foreach (var asset in assets)
            {
                if (string.IsNullOrEmpty(asset.Name) || string.IsNullOrEmpty(asset.DownloadUrl)) continue;
                if (!asset.Name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)) continue;
                if (!AssetMatchesProduct(asset.Name, primaryExecutableFileName)) continue;
                return new ReleaseAssetInfo
                {
                    Name = asset.Name,
                    DownloadUrl = asset.DownloadUrl,
                    Size = asset.Size,
                    IsNoRuntimeVariant = isNoRuntimeVariant,
                };
            }

            return null;
        }

        if (preferNoRuntime)
        {
            return FirstMatching(NoRuntimeSuffix, true)
                   ?? FirstMatching(SelfContainedSuffix, false);
        }

        return FirstMatching(SelfContainedSuffix, false)
               ?? FirstMatching(NoRuntimeSuffix, true);
    }

    public static bool IsRemoteNewerThanCurrent(string remoteTag, string currentDisplayVersion)
    {
        var remote = NormalizeVersionToken(remoteTag.TrimStart('v', 'V'));
        var current = NormalizeVersionToken(currentDisplayVersion.TrimStart('v', 'V'));
        if (!Version.TryParse(remote, out var remoteVersion)) return false;
        if (!Version.TryParse(current, out var currentVersion)) return false;
        return remoteVersion > currentVersion;
    }

    private static string NormalizeVersionToken(string value)
    {
        value = value.Trim();
        if (value.Length == 0) return "0.0.0.0";
        var parts = value.Split('.');
        return parts.Length switch
        {
            1 => $"{parts[0]}.0.0.0",
            2 => $"{parts[0]}.{parts[1]}.0.0",
            3 => $"{parts[0]}.{parts[1]}.{parts[2]}.0",
            _ => value,
        };
    }

    public static string FormatSizeMb(long bytes)
    {
        if (bytes < 0) bytes = 0;
        return $"{bytes / 1024.0 / 1024.0:0.##} MB";
    }

    public static string TruncateNote(string? value, int max = 320)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        value = value.Replace("\r\n", "\n", StringComparison.Ordinal).Trim();
        return value.Length <= max ? value : value[..max] + "…";
    }
}
