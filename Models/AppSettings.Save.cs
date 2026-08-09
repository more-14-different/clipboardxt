using System.IO;
using System.Text.Json;

namespace ClipboardManager;

public partial class AppSettings
{    private static readonly object s_saveLock = new();
    private static CancellationTokenSource? s_pendingSaveCts;
    private static AppSettings? s_pendingSaveSnapshot;

    public void Save()
    {
        try
        {
            var snapshot = ShallowCopy();
            lock (s_saveLock)
            {
                s_pendingSaveSnapshot = snapshot;
                s_pendingSaveCts?.Cancel();
                s_pendingSaveCts = new CancellationTokenSource();
                var token = s_pendingSaveCts.Token;
                var settingsDir = SettingsDir;
                var settingsFile = SettingsFile;
                Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(80, token).ConfigureAwait(false);
                        AppSettings toWrite;
                        lock (s_saveLock)
                        {
                            if (token.IsCancellationRequested) return;
                            toWrite = s_pendingSaveSnapshot;
                            s_pendingSaveSnapshot = null;
                        }
                        if (toWrite != null)
                        {
                            Directory.CreateDirectory(settingsDir);
                            var json = JsonSerializer.Serialize(toWrite, new JsonSerializerOptions { WriteIndented = true });
                            File.WriteAllText(settingsFile, json);
                        }
                    }
                    catch (OperationCanceledException) { }
                    catch { }
                });
            }
        }
        catch { }
    }

    public void SaveSync()
    {
        try
        {
            lock (s_saveLock)
            {
                s_pendingSaveCts?.Cancel();
                s_pendingSaveSnapshot = null;
            }
            Directory.CreateDirectory(SettingsDir);
            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsFile, json);
        }
        catch { }
    }

    public static void FlushPendingSave()
    {
        try
        {
            AppSettings? toWrite;
            lock (s_saveLock)
            {
                s_pendingSaveCts?.Cancel();
                toWrite = s_pendingSaveSnapshot;
                s_pendingSaveSnapshot = null;
            }
            if (toWrite != null)
            {
                Directory.CreateDirectory(SettingsDir);
                var json = JsonSerializer.Serialize(toWrite, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SettingsFile, json);
            }
        }
        catch { }
    }
}

