using ClipboardManager;
using System.IO;

namespace ClipboardX.Tests;

public sealed class FileManagerPathCollectorDopusTests
{
    [Fact]
    public void ParseDopusListerPaths_ReturnsActiveAndPassiveDirectories()
    {
        var root = CreateTempDirectory();
        var active = Directory.CreateDirectory(Path.Combine(root, "active")).FullName;
        var passive = Directory.CreateDirectory(Path.Combine(root, "passive")).FullName;
        var hwnd = new IntPtr(12345);
        try
        {
            var xml = $"""
                <results>
                  <path lister="{(nint)hwnd}" tab_state="1">{active}</path>
                  <path lister="{(nint)hwnd}" tab_state="2">{passive}</path>
                </results>
                """;

            var paths = FileManagerPathCollector.ParseDopusListerPaths(xml, hwnd).ToList();

            Assert.Equal(
                [("Directory Opus (活动)", active), ("Directory Opus (被动)", passive)],
                paths);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ParseDopusListerPaths_AcceptsTruncated32BitHandleId()
    {
        if (IntPtr.Size < 8) return;

        var root = CreateTempDirectory();
        var path = Directory.CreateDirectory(Path.Combine(root, "active")).FullName;
        var hwnd = new IntPtr(0x0000000180001234);
        var truncatedId = unchecked((uint)(nint)hwnd);
        try
        {
            var xml = $"<path lister=\"{truncatedId}\" tab_state=\"1\">{path}</path>";

            var result = Assert.Single(FileManagerPathCollector.ParseDopusListerPaths(xml, hwnd));

            Assert.Equal(("Directory Opus (活动)", path), result);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ParseDopusListerPaths_IgnoresOtherListerHandles()
    {
        var root = CreateTempDirectory();
        try
        {
            var xml = $"<path lister=\"999\" tab_state=\"1\">{root}</path>";

            var paths = FileManagerPathCollector.ParseDopusListerPaths(xml, new IntPtr(12345));

            Assert.Empty(paths);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ParseDopusListerPaths_FiltersMissingDirectories()
    {
        var root = CreateTempDirectory();
        var missing = Path.Combine(root, "missing");
        var hwnd = new IntPtr(12345);
        try
        {
            var xml = $"<path lister=\"{(nint)hwnd}\" tab_state=\"1\">{missing}</path>";

            var paths = FileManagerPathCollector.ParseDopusListerPaths(xml, hwnd);

            Assert.Empty(paths);
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
