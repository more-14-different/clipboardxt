using ClipboardManager;
using System.IO;

namespace ClipboardX.Tests;

public sealed class FileJumpCandidateAccumulatorTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "ClipboardX-CandidateTests-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void Add_NormalizesAndDeduplicatesWithoutCheckingExistence()
    {
        var directory = CreateDirectory("Alpha");
        var accumulator = new FileJumpCandidateAccumulator();

        accumulator.Add("first", directory);
        accumulator.Add("duplicate", directory.ToUpperInvariant());
        var missing = Path.Combine(_root, "missing");
        accumulator.Add("missing", missing);

        Assert.Collection(
            accumulator.Result,
            candidate =>
            {
                Assert.Equal("first", candidate.Label);
                Assert.Equal(Path.GetFullPath(directory), candidate.Path);
            },
            candidate =>
            {
                Assert.Equal("missing", candidate.Label);
                Assert.Equal(Path.GetFullPath(missing), candidate.Path);
            });
        Assert.Equal(2, accumulator.AddedCount);
    }

    [Fact]
    public void AppendFavoriteFolders_PrefersRecentFoldersAndSkipsBlankEntries()
    {
        var memory = CreateDirectory("Memory");
        var first = CreateDirectory("First");
        var second = CreateDirectory("Second");
        var accumulator = new FileJumpCandidateAccumulator();

        accumulator.AppendFavoriteFolders(memory, [first, "  ", second]);

        Assert.Collection(
            accumulator.Result,
            item =>
            {
                Assert.Equal("常用路径1", item.Label);
                Assert.Equal(Path.GetFullPath(first), item.Path);
            },
            item =>
            {
                Assert.Equal("常用路径2", item.Label);
                Assert.Equal(Path.GetFullPath(second), item.Path);
            });
    }

    [Fact]
    public void AppendFavoriteFolders_UsesMemoryWhenRecentFoldersAreUnavailable()
    {
        var memory = CreateDirectory("Memory");
        var accumulator = new FileJumpCandidateAccumulator();

        accumulator.AppendFavoriteFolders(memory, null);

        var candidate = Assert.Single(accumulator.Result);
        Assert.Equal("常用路径1", candidate.Label);
        Assert.Equal(Path.GetFullPath(memory), candidate.Path);
    }

    private string CreateDirectory(string name)
    {
        var path = Path.Combine(_root, name);
        Directory.CreateDirectory(path);
        return path;
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, recursive: true);
        }
        catch
        {
            // Best effort test cleanup.
        }
    }
}
