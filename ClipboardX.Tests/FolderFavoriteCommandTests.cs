using System.IO;
using ClipboardManager;

namespace ClipboardX.Tests;

public sealed class FolderFavoriteCommandTests
{
    [Theory]
    [InlineData(true, false, false, true)]
    [InlineData(true, false, true, false)]
    [InlineData(true, true, false, false)]
    [InlineData(false, false, false, false)]
    public void ShouldRequestElevation_NeverElevatesFolderFavoriteCommand(
        bool runAsAdministrator,
        bool isElevated,
        bool isFolderFavoriteCommand,
        bool expected)
    {
        Assert.Equal(expected, ProcessElevation.ShouldRequestElevation(
            runAsAdministrator,
            isElevated,
            isFolderFavoriteCommand));
    }

    [Fact]
    public void TryParse_AcceptsExistingDirectory()
    {
        var expected = Path.GetFullPath(Path.GetTempPath());

        var parsed = FolderFavoriteCommand.TryParse(
            [FolderFavoriteCommand.AddArgument, expected], out var actual);

        Assert.True(parsed);
        Assert.Equal(
            expected.TrimEnd(Path.DirectorySeparatorChar),
            actual.TrimEnd(Path.DirectorySeparatorChar),
            StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryParse_RejectsMissingOrNonexistentDirectory()
    {
        Assert.False(FolderFavoriteCommand.TryParse(
            [FolderFavoriteCommand.AddArgument], out _));
        Assert.False(FolderFavoriteCommand.TryParse(
            [FolderFavoriteCommand.AddArgument, Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))],
            out _));
    }

    [Fact]
    public void TryAdd_IsIdempotentAcrossTrailingDirectorySeparator()
    {
        var path = Path.GetFullPath(Path.GetTempPath())
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var settings = new AppSettings
        {
            FolderFavorites =
            [
                new FolderFavoriteEntry
                {
                    Phrase = "existing",
                    Path = path + Path.DirectorySeparatorChar,
                },
            ],
        };

        var success = FolderFavoriteCommand.TryAdd(settings, path, out var response);

        Assert.True(success);
        Assert.Equal("exists", response);
        Assert.Single(settings.FolderFavorites);
        Assert.Equal("existing", settings.FolderFavorites[0].Phrase);
    }
}
