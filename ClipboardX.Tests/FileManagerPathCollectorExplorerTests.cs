using ClipboardManager;
using System.IO;

namespace ClipboardX.Tests;

public sealed class FileManagerPathCollectorExplorerTests
{
    [Fact]
    public void MergeExplorerComPathWithUiPath_HandlesMissingSources()
    {
        Assert.Null(FileManagerPathCollector.MergeExplorerComPathWithUiPath(null, null));
        Assert.Equal(@"C:\Com", FileManagerPathCollector.MergeExplorerComPathWithUiPath(@"C:\Com", null));
        Assert.Equal(@"C:\Uia", FileManagerPathCollector.MergeExplorerComPathWithUiPath(null, @"C:\Uia"));
    }

    [Fact]
    public void MergeExplorerComPathWithUiPath_PreservesComPathWhenEquivalent()
    {
        var root = CreateTempDirectory();
        try
        {
            var comPath = root + Path.DirectorySeparatorChar;

            var result = FileManagerPathCollector.MergeExplorerComPathWithUiPath(comPath, root);

            Assert.Equal(comPath, result);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void MergeExplorerComPathWithUiPath_PrefersMoreSpecificDescendant()
    {
        var root = CreateTempDirectory();
        var child = Directory.CreateDirectory(Path.Combine(root, "child")).FullName;
        try
        {
            Assert.Equal(child, FileManagerPathCollector.MergeExplorerComPathWithUiPath(root, child));
            Assert.Equal(child, FileManagerPathCollector.MergeExplorerComPathWithUiPath(child, root));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void MergeExplorerComPathWithUiPath_PrefersNonRootPathOverDriveRoot()
    {
        var root = CreateTempDirectory();
        var driveRoot = Path.GetPathRoot(root)!;
        try
        {
            Assert.Equal(root, FileManagerPathCollector.MergeExplorerComPathWithUiPath(driveRoot, root));
            Assert.Equal(root, FileManagerPathCollector.MergeExplorerComPathWithUiPath(root, driveRoot));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void MergeExplorerComPathWithUiPath_PrefersUiaForUnrelatedPaths()
    {
        var root = CreateTempDirectory();
        var comPath = Directory.CreateDirectory(Path.Combine(root, "com")).FullName;
        var uiaPath = Directory.CreateDirectory(Path.Combine(root, "uia")).FullName;
        try
        {
            Assert.Equal(uiaPath, FileManagerPathCollector.MergeExplorerComPathWithUiPath(comPath, uiaPath));
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
