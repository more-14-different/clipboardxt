using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ClipboardManager;

/// <summary>通过 GitHub Releases 检查版本、下载 zip 并在退出进程后替换文件并重启。</summary>
internal static partial class GitHubUpdateService
{
    private static readonly Lazy<HttpClient> HttpLazy = new(CreateHttpClient);

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd(
            $"ClipboardX/{AppInfo.DisplayVersion} ({AppInfo.GitHubUrl})");
        client.DefaultRequestHeaders.Add("Accept", "application/vnd.github+json");
        return client;
    }

    private static HttpClient Http => HttpLazy.Value;

    public static async Task<LatestReleaseInfo> FetchLatestReleaseAsync(CancellationToken ct = default)
    {
        var (owner, repo) = AppInfo.ParseGitHubRepo();
        var url =
            $"https://api.github.com/repos/{Uri.EscapeDataString(owner)}/{Uri.EscapeDataString(repo)}/releases/latest";
        using var response = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct)
            .ConfigureAwait(false);
        var json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            throw new IOException($"GitHub API {(int)response.StatusCode}：{TruncateNote(json, 200)}");

        var document = JsonSerializer.Deserialize<GitHubReleaseDocument>(json);
        if (document?.TagName is not { Length: > 0 } tag)
            throw new IOException("发行版数据异常：缺少 tag_name。");

        var candidates = document.Assets?
            .Select(asset => new ReleaseAssetCandidate
            {
                Name = asset.Name ?? "",
                DownloadUrl = asset.BrowserDownloadUrl ?? "",
                Size = asset.Size,
            })
            .ToList();
        var chosen = PickZipAsset(candidates, PreferNoRuntimeZip(), AppInfo.PrimaryExecutableFileName);
        if (chosen == null)
        {
            throw new IOException(
                "该发行版未找到与当前程序匹配的 win-x64 zip（no-runtime / self-contained）。发行页上需存在与主 exe 前缀一致的包。");
        }

        return new LatestReleaseInfo
        {
            TagName = tag,
            Body = document.Body ?? "",
            ChosenAsset = chosen.Value,
        };
    }

    private sealed class GitHubReleaseDocument
    {
        [JsonPropertyName("tag_name")]
        public string? TagName { get; set; }

        [JsonPropertyName("body")]
        public string? Body { get; set; }

        [JsonPropertyName("assets")]
        public List<GitHubAssetDocument>? Assets { get; set; }
    }

    private sealed class GitHubAssetDocument
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("browser_download_url")]
        public string? BrowserDownloadUrl { get; set; }

        [JsonPropertyName("size")]
        public long Size { get; set; }
    }
}
