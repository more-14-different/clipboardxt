using System.IO;
using ClipboardManager;

namespace ClipboardX.Tests;

public sealed class AltVTextPasteSessionTests
{
    [Fact]
    public async Task ExecuteStandaloneClipboardPasteAsync_DisposesProviderAfterSuccess()
    {
        var fixture = ProviderSessionFixture.Create(success: true);
        var events = new List<string>();
        var session = CreateSession(
            fixture.Session,
            sendCtrlV: () => events.Add("ctrlv"));

        try
        {
            var result = await session.ExecuteStandaloneClipboardPasteAsync(
                "folder",
                "test",
                maxRetries: 1,
                delayMs: 1,
                markSelfWroteClipboard: () => events.Add("mark"));

            Assert.True(result.Success);
            Assert.False(result.UsedNonClipboardTextInsert);
            Assert.Equal(["mark", "ctrlv"], events);
            fixture.AssertProtocolFilesDeleted();
        }
        finally
        {
            fixture.Cleanup();
        }
    }

    [Fact]
    public async Task ExecuteStandaloneClipboardPasteAsync_DisposesProviderWhenDispatchThrows()
    {
        var fixture = ProviderSessionFixture.Create(success: true);
        var session = CreateSession(
            fixture.Session,
            sendCtrlV: () => throw new InvalidOperationException("dispatch failed"));

        try
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                session.ExecuteStandaloneClipboardPasteAsync(
                    "folder",
                    "test",
                    maxRetries: 1,
                    delayMs: 1,
                    markSelfWroteClipboard: () => { }));

            fixture.AssertProtocolFilesDeleted();
        }
        finally
        {
            fixture.Cleanup();
        }
    }

    private static AltVTextPasteSession CreateSession(
        AltVClipboardProvider.Session providerSession,
        Action sendCtrlV)
    {
        return new AltVTextPasteSession(
            IntPtr.Zero,
            useExternalClipboardProvider: true,
            PasteSimulationModes.CtrlV,
            (_, _, _) => Task.FromResult(
                new AltVTextPasteSession.ClipboardWriteResult(false, false)),
            _ => false,
            _ => false,
            sendCtrlV,
            sendShiftInsertPaste: () => { },
            startProviderSessionAsync: _ => Task.FromResult(providerSession));
    }

    private sealed class ProviderSessionFixture
    {
        private ProviderSessionFixture(
            string directory,
            string requestFile,
            string resultFile,
            string stopFile,
            AltVClipboardProvider.Session session)
        {
            Directory = directory;
            RequestFile = requestFile;
            ResultFile = resultFile;
            StopFile = stopFile;
            Session = session;
        }

        private string Directory { get; }
        private string RequestFile { get; }
        private string ResultFile { get; }
        private string StopFile { get; }
        internal AltVClipboardProvider.Session Session { get; }

        internal static ProviderSessionFixture Create(bool success)
        {
            var directory = Path.Combine(
                Path.GetTempPath(),
                "ClipboardX.Tests",
                Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(directory);
            var requestFile = Path.Combine(directory, "request.txt");
            var resultFile = Path.Combine(directory, "result.json");
            var stopFile = Path.Combine(directory, "stop.signal");
            File.WriteAllText(requestFile, "folder");
            File.WriteAllText(resultFile, "{}");
            File.WriteAllText(stopFile, "");

            var session = new AltVClipboardProvider.Session(
                new AltVClipboardProvider.Result(success, false, ""),
                process: null,
                requestFile,
                resultFile,
                stopFile);
            return new ProviderSessionFixture(
                directory,
                requestFile,
                resultFile,
                stopFile,
                session);
        }

        internal void AssertProtocolFilesDeleted()
        {
            Assert.False(File.Exists(RequestFile));
            Assert.False(File.Exists(ResultFile));
            Assert.False(File.Exists(StopFile));
        }

        internal void Cleanup()
        {
            try
            {
                if (System.IO.Directory.Exists(Directory))
                    System.IO.Directory.Delete(Directory, recursive: true);
            }
            catch
            {
                // Test cleanup should not mask the assertion result.
            }
        }
    }
}
