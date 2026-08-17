using ClipboardManager.Models;
using Microsoft.Data.Sqlite;
using System.IO;

namespace ClipboardManager.Tests;

public sealed class ClipboardHistoryStoreSearchTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "ClipboardX.Tests",
        Guid.NewGuid().ToString("N"));
    private readonly string _databasePath;

    public ClipboardHistoryStoreSearchTests()
    {
        Directory.CreateDirectory(_directory);
        _databasePath = Path.Combine(_directory, "history.db");
    }

    [Fact]
    public void Search_LongTokenUsesFtsIncludingFilePaths()
    {
        var store = CreateStore();
        Assert.True(store.TryInsert(new ClipboardEntry
        {
            Type = EntryType.Files,
            FilePaths = [@"C:\资料\quarterly-report-final.pdf"],
            CopiedAt = DateTime.Now
        }));

        var result = store.Search("report-final", null, 10);

        var entry = Assert.Single(result);
        Assert.Equal(EntryType.Files, entry.Type);
        Assert.Contains("quarterly-report-final.pdf", entry.FilePaths![0]);
    }

    [Fact]
    public void Search_ImageFilterIncludesClipboardImagesAndImageFilesAndHydratesBlob()
    {
        var store = CreateStore();
        var png = new byte[] { 1, 2, 3, 4 };
        Assert.True(store.TryInsert(new ClipboardEntry
        {
            Type = EntryType.Image,
            ImageData = png,
            ImageWidth = 10,
            ImageHeight = 20,
            CopiedAt = DateTime.Now
        }));
        Assert.True(store.TryInsert(new ClipboardEntry
        {
            Type = EntryType.Files,
            FilePaths = [@"C:\images\photo.PNG"],
            CopiedAt = DateTime.Now.AddSeconds(-1)
        }));
        Assert.True(store.TryInsert(new ClipboardEntry
        {
            Type = EntryType.Files,
            FilePaths = [@"C:\docs\notes.txt"],
            CopiedAt = DateTime.Now.AddSeconds(-2)
        }));

        var result = store.Search("", EntryType.Image, 10);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, entry => entry.Type == EntryType.Image && entry.ImageData!.SequenceEqual(png));
        Assert.Contains(result, entry => entry.IsImageFile);
    }

    [Fact]
    public void Search_ShortcutOnlyFilterIsAppliedBeforeLimit()
    {
        var store = CreateStore();
        Assert.True(store.TryInsert(new ClipboardEntry
        {
            Type = EntryType.Text,
            TextContent = "older phrase",
            ShortcutPhrase = "op",
            CopiedAt = DateTime.Now.AddHours(-1)
        }));

        for (var i = 0; i < 110; i++)
        {
            Assert.True(store.TryInsert(new ClipboardEntry
            {
                Type = EntryType.Text,
                TextContent = $"new item {i}",
                CopiedAt = DateTime.Now.AddSeconds(i)
            }));
        }

        var result = store.Search("", null, 100, shortcutPhraseOnly: true);

        var entry = Assert.Single(result);
        Assert.Equal("op", entry.ShortcutPhrase);
    }

    [Fact]
    public void Search_FindsShortcutPhraseValue()
    {
        var store = CreateStore();
        Assert.True(store.TryInsert(new ClipboardEntry
        {
            Type = EntryType.Text,
            TextContent = "unrelated content",
            ShortcutPhrase = "deploy-production",
            CopiedAt = DateTime.Now
        }));

        var entry = Assert.Single(store.Search("deploy-production", null, 10));

        Assert.Equal("deploy-production", entry.ShortcutPhrase);
    }

    [Theory]
    [InlineData("网址")]
    [InlineData("URL")]
    public void Search_FindsExistingWebUrlsByDerivedMetadata(string query)
    {
        var store = CreateStore();
        Assert.True(store.TryInsert(new ClipboardEntry
        {
            Type = EntryType.Text,
            TextContent = "https://example.com/existing",
            CopiedAt = DateTime.Now
        }));
        Assert.True(store.TryInsert(new ClipboardEntry
        {
            Type = EntryType.Text,
            TextContent = "https://example.com/path with space",
            CopiedAt = DateTime.Now.AddSeconds(-1)
        }));

        var entry = Assert.Single(store.Search(query, null, 10));

        Assert.Equal("https://example.com/existing", entry.TextContent);
        Assert.True(entry.IsWebUrl);
    }

    [Fact]
    public void Search_XiaoheFindsLegacyWebUrlWithoutDerivedMetadataInPersistedPinyin()
    {
        var store = CreateStore();
        Assert.True(store.TryInsert(new ClipboardEntry
        {
            Type = EntryType.Text,
            TextContent = "https://example.com/legacy",
            CopiedAt = DateTime.Now
        }));
        ClearPersistedPinyinBlob();

        var entry = Assert.Single(store.Search("whvi", null, 10));

        Assert.Equal("https://example.com/legacy", entry.TextContent);
        Assert.True(entry.IsWebUrl);
    }

    [Fact]
    public void Search_FindsArchivedWebUrlByDerivedMetadata()
    {
        var store = CreateStore();
        Assert.True(store.TryInsert(new ClipboardEntry
        {
            Type = EntryType.Text,
            TextContent = "https://example.com/archived",
            CopiedAt = DateTime.Now.AddHours(-2)
        }));
        Assert.True(store.TryInsert(new ClipboardEntry
        {
            Type = EntryType.Text,
            TextContent = "newest ordinary text",
            CopiedAt = DateTime.Now
        }));
        store.ArchiveExcess(1);

        var entry = Assert.Single(store.Search("网址", null, 10));

        Assert.True(entry.IsArchived);
        Assert.True(entry.IsWebUrl);
    }

    [Fact]
    public void Search_AnchorVerificationContinuesPastFormerCandidateCap()
    {
        var store = CreateStore();
        Assert.True(store.TryInsert(new ClipboardEntry
        {
            Type = EntryType.Text,
            TextContent = "target winner",
            CopiedAt = DateTime.Now.AddDays(-1)
        }));

        for (var i = 0; i < 2050; i++)
        {
            Assert.True(store.TryInsert(new ClipboardEntry
            {
                Type = EntryType.Text,
                TextContent = $"target decoy {i}",
                ShortcutPhrase = "prefix",
                CopiedAt = DateTime.Now.AddSeconds(i)
            }));
        }

        var result = store.Search(" target", null, 10);

        var entry = Assert.Single(result);
        Assert.Equal("target winner", entry.TextContent);
    }

    [Fact]
    public void SchemaMigrationIsVersionedAndStableAcrossReopen()
    {
        _ = CreateStore();
        var firstVersion = ReadUserVersion();
        _ = CreateStore();
        var secondVersion = ReadUserVersion();

        Assert.Equal(2, firstVersion);
        Assert.Equal(firstVersion, secondVersion);
    }

    [Fact]
    public void Search_FindsAndHydratesColdArchiveEntries()
    {
        var store = CreateStore();
        var png = new byte[] { 9, 8, 7, 6 };
        Assert.True(store.TryInsert(new ClipboardEntry
        {
            Type = EntryType.Image,
            ImageData = png,
            ImageWidth = 30,
            ImageHeight = 40,
            CopiedAt = DateTime.Now.AddHours(-2)
        }));
        Assert.True(store.TryInsert(new ClipboardEntry
        {
            Type = EntryType.Text,
            TextContent = "keep this hot",
            CopiedAt = DateTime.Now
        }));
        store.ArchiveExcess(1);

        // Reopen also verifies that existing archive bucket indexes are discovered at startup.
        store = CreateStore();
        Assert.Empty(store.Search("im", EntryType.Image, 10));
        Assert.Empty(store.Search("image", EntryType.Image, 10, searchColdArchives: false));
        var result = store.Search("image", EntryType.Image, 10);

        var entry = Assert.Single(result);
        Assert.True(entry.IsArchived);
        Assert.Null(entry.PersistedId);
        Assert.Equal(png, entry.ImageData);
    }

    [Fact]
    public void RestoreArchivedEntryMovesItBackToHotHistory()
    {
        var store = CreateStore();
        Assert.True(store.TryInsert(new ClipboardEntry
        {
            Type = EntryType.Text,
            TextContent = "cold searchable marker",
            CopiedAt = DateTime.Now.AddHours(-2)
        }));
        Assert.True(store.TryInsert(new ClipboardEntry
        {
            Type = EntryType.Text,
            TextContent = "newest",
            CopiedAt = DateTime.Now
        }));
        store.ArchiveExcess(1);
        var archived = Assert.Single(store.Search("searchable marker", null, 10));

        Assert.True(store.TryRestoreArchived(archived));

        Assert.False(archived.IsArchived);
        Assert.NotNull(archived.PersistedId);
        Assert.Single(store.Search("searchable marker", null, 10));
    }

    [Fact]
    public void Search_HonorsCancellationBeforeDatabaseWorkStarts()
    {
        var store = CreateStore();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.Throws<OperationCanceledException>(
            () => store.Search("cancelled query", null, 10, cancellationToken: cancellation.Token));
    }

    [Theory]
    [InlineData("%")]
    [InlineData("_")]
    [InlineData("\\")]
    public void Search_LikeSpecialCharacterIsMatchedLiterally(string token)
    {
        var store = CreateStore();
        Assert.True(store.TryInsert(new ClipboardEntry
        {
            Type = EntryType.Text,
            TextContent = $"target contains {token} marker",
            CopiedAt = DateTime.Now
        }));
        Assert.True(store.TryInsert(new ClipboardEntry
        {
            Type = EntryType.Text,
            TextContent = "decoy contains ordinary marker",
            CopiedAt = DateTime.Now.AddSeconds(-1)
        }));

        var result = store.Search(token, null, 10);

        var entry = Assert.Single(result);
        Assert.Equal($"target contains {token} marker", entry.TextContent);
    }

    private ClipboardHistoryStore CreateStore()
    {
        ClipboardHistoryStore.PinyinFilterMode = PinyinFilterModes.Xiaohe;
        return new ClipboardHistoryStore(_databasePath);
    }

    private int ReadUserVersion()
    {
        using var conn = new SqliteConnection($"Data Source={_databasePath};Mode=ReadOnly");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA user_version";
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    private void ClearPersistedPinyinBlob()
    {
        using var conn = new SqliteConnection($"Data Source={_databasePath}");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE clipboard_history SET pinyin_blob = ''";
        cmd.ExecuteNonQuery();
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(_directory, recursive: true); }
        catch { /* best effort test cleanup */ }
    }
}
