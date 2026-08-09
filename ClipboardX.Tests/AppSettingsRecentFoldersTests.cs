using ClipboardManager;
using System.IO;

namespace ClipboardX.Tests;

public sealed class AppSettingsRecentFoldersTests
{
    [Fact]
    public void RecordRecentFolderUse_DoesNotRequireDirectoryToExist()
    {
        var missing = Path.Combine(
            Path.GetTempPath(),
            "ClipboardX-Missing-" + Guid.NewGuid().ToString("N"));
        var settings = new AppSettings();

        settings.RecordRecentFolderUse(missing);

        Assert.Equal(Path.GetFullPath(missing), settings.LastFileDialogFolder);
        Assert.Equal([Path.GetFullPath(missing)], settings.RecentFileDialogFolders);
    }

    [Fact]
    public void ApplyRecentFolderLimit_ClampsLimitAndDropsOldestEntries()
    {
        var settings = new AppSettings
        {
            RecentFolderMaxCount = 2,
            RecentFileDialogFolders = [@"D:\Newest", @"D:\Middle", @"D:\Oldest"]
        };

        settings.ApplyRecentFolderLimit();

        Assert.Equal([@"D:\Newest", @"D:\Middle"], settings.RecentFileDialogFolders);
        Assert.Equal(@"D:\Newest", settings.LastFileDialogFolder);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(51, 50)]
    public void ApplyRecentFolderLimit_ClampsConfiguredRange(int configured, int expected)
    {
        var settings = new AppSettings { RecentFolderMaxCount = configured };

        settings.ApplyRecentFolderLimit();

        Assert.Equal(expected, settings.RecentFolderMaxCount);
    }
}
