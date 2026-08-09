using System.IO;
using System.IO.Compression;
using System.Net.Http;

namespace ClipboardManager;

internal static partial class GitHubUpdateService
{
    public static async Task DownloadToFileAsync(
        string downloadUrl,
        string filePath,
        CancellationToken ct = default)
    {
        using var response = await Http.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, ct)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using var file = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
        await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        await stream.CopyToAsync(file, ct).ConfigureAwait(false);
    }

    public static void ExtractZipToDirectory(string zipPath, string destinationDirectory)
    {
        Directory.CreateDirectory(destinationDirectory);
        ZipFile.ExtractToDirectory(zipPath, destinationDirectory, overwriteFiles: true);
    }
}
