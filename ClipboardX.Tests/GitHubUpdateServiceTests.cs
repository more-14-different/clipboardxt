using ClipboardManager;

namespace ClipboardX.Tests;

public sealed class GitHubUpdateServiceTests
{
    private static readonly GitHubUpdateService.ReleaseAssetCandidate[] ProductAssets =
    [
        Asset("ClipboardX-clipboard-1.9.0-win-x64-no-runtime.zip", 11),
        Asset("ClipboardX-filejump-1.9.0-win-x64-self-contained.zip", 12),
        Asset("ClipboardX-1.9.0-win-x64-self-contained.zip", 13),
        Asset("ClipboardX-filejump-1.9.0-win-x64-no-runtime.zip", 14),
        Asset("ClipboardX-1.9.0-win-x64-no-runtime.zip", 15),
        Asset("ClipboardX-clipboard-1.9.0-win-x64-self-contained.zip", 16)
    ];

    [Theory]
    [InlineData("v1.8.1", "1.8.0", true)]
    [InlineData("1.9", "1.8.9", true)]
    [InlineData("1.8", "1.8.0", false)]
    [InlineData("1.7.9", "1.8.0", false)]
    [InlineData("invalid", "1.8.0", false)]
    [InlineData("1.9.0-beta", "1.8.0", false)]
    public void IsRemoteNewerThanCurrent_ComparesNormalizedVersions(
        string remote,
        string current,
        bool expected)
    {
        Assert.Equal(expected, GitHubUpdateService.IsRemoteNewerThanCurrent(remote, current));
    }

    [Theory]
    [InlineData("ClipboardX.exe", true, "ClipboardX-1.9.0-win-x64-no-runtime.zip", true)]
    [InlineData("ClipboardX.exe", false, "ClipboardX-1.9.0-win-x64-self-contained.zip", false)]
    [InlineData("ClipboardX-clipboard.exe", true, "ClipboardX-clipboard-1.9.0-win-x64-no-runtime.zip", true)]
    [InlineData("ClipboardX-clipboard.exe", false, "ClipboardX-clipboard-1.9.0-win-x64-self-contained.zip", false)]
    [InlineData("ClipboardX-filejump.exe", true, "ClipboardX-filejump-1.9.0-win-x64-no-runtime.zip", true)]
    [InlineData("ClipboardX-filejump.exe", false, "ClipboardX-filejump-1.9.0-win-x64-self-contained.zip", false)]
    public void PickZipAsset_SelectsCurrentProductAndPreferredRuntime(
        string executableName,
        bool preferNoRuntime,
        string expectedName,
        bool expectedNoRuntime)
    {
        var selected = GitHubUpdateService.PickZipAsset(ProductAssets, preferNoRuntime, executableName);

        Assert.NotNull(selected);
        Assert.Equal(expectedName, selected.Value.Name);
        Assert.Equal(expectedNoRuntime, selected.Value.IsNoRuntimeVariant);
        Assert.StartsWith("https://example.test/", selected.Value.DownloadUrl, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(true, "ClipboardX-clipboard-1.9.0-win-x64-self-contained.zip", false)]
    [InlineData(false, "ClipboardX-clipboard-1.9.0-win-x64-no-runtime.zip", true)]
    public void PickZipAsset_FallsBackWhenPreferredRuntimeIsMissing(
        bool preferNoRuntime,
        string availableName,
        bool expectedNoRuntime)
    {
        var assets = new[]
        {
            Asset(availableName, 20)
        };

        var selected = GitHubUpdateService.PickZipAsset(
            assets,
            preferNoRuntime,
            "ClipboardX-clipboard.exe");

        Assert.NotNull(selected);
        Assert.Equal(availableName, selected.Value.Name);
        Assert.Equal(expectedNoRuntime, selected.Value.IsNoRuntimeVariant);
        Assert.Equal(20, selected.Value.Size);
    }

    [Fact]
    public void PickZipAsset_RejectsOtherProductsAndInvalidAssets()
    {
        var assets = new[]
        {
            Asset("ClipboardX-filejump-1.9.0-win-x64-no-runtime.zip", 10),
            new GitHubUpdateService.ReleaseAssetCandidate
            {
                Name = "ClipboardX-1.9.0-win-x64-no-runtime.zip",
                DownloadUrl = "",
                Size = 10
            },
            Asset("ClipboardX-1.9.0-linux-x64.zip", 10)
        };

        var selected = GitHubUpdateService.PickZipAsset(
            assets,
            preferNoRuntime: true,
            "ClipboardX-clipboard.exe");

        Assert.Null(selected);
    }

    [Theory]
    [InlineData(-1, "0 MB")]
    [InlineData(0, "0 MB")]
    [InlineData(1048576, "1 MB")]
    [InlineData(1572864, "1.5 MB")]
    public void FormatSizeMb_FormatsNonNegativeMegabytes(long bytes, string expected)
    {
        Assert.Equal(expected, GitHubUpdateService.FormatSizeMb(bytes));
    }

    [Fact]
    public void TruncateNote_NormalizesLineEndingsAndAppendsEllipsis()
    {
        Assert.Equal("line1\nline2", GitHubUpdateService.TruncateNote("  line1\r\nline2  "));
        Assert.Equal("line1…", GitHubUpdateService.TruncateNote("line1 line2", 5));
        Assert.Equal("", GitHubUpdateService.TruncateNote("   "));
    }

    private static GitHubUpdateService.ReleaseAssetCandidate Asset(string name, long size) => new()
    {
        Name = name,
        DownloadUrl = "https://example.test/" + name,
        Size = size
    };
}
