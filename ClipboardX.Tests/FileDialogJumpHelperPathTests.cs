using ClipboardManager;
using System.IO;

namespace ClipboardX.Tests;

public sealed class FileDialogJumpHelperPathTests
{
    [Fact]
    public void TryNormalizeToExistingDirectory_NormalizesDirectoryAndQuotedPath()
    {
        var root = CreateTempDirectory();
        try
        {
            Assert.True(FileDialogJumpHelper.TryNormalizeToExistingDirectory(root, out var plain));
            Assert.Equal(Path.GetFullPath(root), plain);

            Assert.True(FileDialogJumpHelper.TryNormalizeToExistingDirectory($"\"{root}\"", out var quoted));
            Assert.Equal(Path.GetFullPath(root), quoted);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void TryNormalizeToExistingDirectory_UsesParentForFilePath()
    {
        var root = CreateTempDirectory();
        var file = Path.Combine(root, "entry.txt");
        try
        {
            File.WriteAllText(file, "test");

            Assert.True(FileDialogJumpHelper.TryNormalizeToExistingDirectory(file, out var normalized));
            Assert.Equal(Path.GetFullPath(root), normalized);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("relative-folder")]
    public void TryNormalizeToExistingDirectory_RejectsInvalidOrRelativePath(string? value)
    {
        Assert.False(FileDialogJumpHelper.TryNormalizeToExistingDirectory(value, out var normalized));
        Assert.Empty(normalized);
    }

    [Fact]
    public void TryWpsBreadcrumbTextToFolder_ResolvesExistingBreadcrumb()
    {
        var root = CreateTempDirectory();
        var child = Directory.CreateDirectory(Path.Combine(root, "child")).FullName;
        try
        {
            var breadcrumb = Path.GetFullPath(child).Replace("\\", " > ", StringComparison.Ordinal);

            Assert.True(FileDialogJumpHelper.TryWpsBreadcrumbTextToFolder(breadcrumb, out var folder));
            Assert.Equal(Path.GetFullPath(child), folder);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "ClipboardX.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
