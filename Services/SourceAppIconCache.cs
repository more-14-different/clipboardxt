using System.Collections.Concurrent;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using Microsoft.Data.Sqlite;

namespace ClipboardManager.Services;

internal static class SourceAppIconCache
{
    private sealed record CacheEntry(BitmapSource? Icon);

    private static readonly ConcurrentDictionary<string, CacheEntry> MemoryCache =
        new(StringComparer.OrdinalIgnoreCase);

    public static BitmapSource? GetIconSource(string? exePath)
    {
        if (string.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath)) return null;
        return MemoryCache.GetOrAdd(
            exePath,
            static path => new CacheEntry(LoadIconSource(path))).Icon;
    }

    public static byte[]? GetOrCreateIconPng(string? exePath, SqliteConnection? conn = null)
    {
        if (string.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath)) return null;

        var key = BuildIconKey(exePath);
        if (key == null) return null;

        if (conn != null)
        {
            var cached = TryReadIcon(conn, key);
            if (cached is { Length: > 0 }) return cached;
        }

        var png = ExtractIconPng(exePath);
        if (png is { Length: > 0 } && conn != null)
            TryWriteIcon(conn, key, exePath, png);
        return png;
    }

    /// <summary>仅从 SQLite 缓存读取，不探测或打开来源 exe；供列表查询/渲染路径使用。</summary>
    internal static byte[]? TryGetCachedIconPng(string? exePath, SqliteConnection conn)
    {
        if (string.IsNullOrWhiteSpace(exePath)) return null;
        try
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText =
                "SELECT icon_png FROM app_icon_cache " +
                "WHERE exe_path = @path COLLATE NOCASE ORDER BY last_seen_ms DESC LIMIT 1";
            cmd.Parameters.AddWithValue("@path", exePath.Trim());
            return cmd.ExecuteScalar() is byte[] bytes ? bytes : null;
        }
        catch
        {
            return null;
        }
    }

    public static BitmapSource? DecodeIcon(byte[]? png, string? exePathFallback = null)
    {
        if (png is { Length: > 0 })
        {
            try
            {
                using var ms = new MemoryStream(png);
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.StreamSource = ms;
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.DecodePixelWidth = 32;
                bmp.EndInit();
                bmp.Freeze();
                return bmp;
            }
            catch
            {
                // Fall through to shell extraction.
            }
        }

        return GetIconSource(exePathFallback);
    }

    private static BitmapSource? LoadIconSource(string exePath)
    {
        var png = ExtractIconPng(exePath);
        return DecodeIcon(png);
    }

    private static byte[]? ExtractIconPng(string exePath)
    {
        try
        {
            using var icon = Icon.ExtractAssociatedIcon(exePath);
            if (icon == null) return null;
            using var bmp = icon.ToBitmap();
            using var ms = new MemoryStream();
            bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
            return ms.ToArray();
        }
        catch
        {
            return null;
        }
    }

    private static string? BuildIconKey(string exePath)
    {
        try
        {
            var full = Path.GetFullPath(exePath);
            var info = new FileInfo(full);
            if (!info.Exists) return null;
            return $"{full}|{info.Length}|{info.LastWriteTimeUtc.Ticks}";
        }
        catch
        {
            return null;
        }
    }

    private static byte[]? TryReadIcon(SqliteConnection conn, string key)
    {
        try
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT icon_png FROM app_icon_cache WHERE icon_key = @key";
            cmd.Parameters.AddWithValue("@key", key);
            var value = cmd.ExecuteScalar();
            return value is byte[] bytes ? bytes : null;
        }
        catch
        {
            return null;
        }
    }

    private static void TryWriteIcon(SqliteConnection conn, string key, string exePath, byte[] png)
    {
        try
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText =
                """
                INSERT OR REPLACE INTO app_icon_cache
                  (icon_key, exe_path, file_mtime_utc_ticks, file_size, icon_png, last_seen_ms)
                VALUES
                  (@key, @path, @mtime, @size, @png, @lastSeen)
                """;
            var info = new FileInfo(exePath);
            cmd.Parameters.AddWithValue("@key", key);
            cmd.Parameters.AddWithValue("@path", Path.GetFullPath(exePath));
            cmd.Parameters.AddWithValue("@mtime", info.LastWriteTimeUtc.Ticks);
            cmd.Parameters.AddWithValue("@size", info.Length);
            cmd.Parameters.AddWithValue("@png", png);
            cmd.Parameters.AddWithValue("@lastSeen", DateTimeOffset.Now.ToUnixTimeMilliseconds());
            cmd.ExecuteNonQuery();
        }
        catch
        {
            // Icon cache is best-effort only.
        }
    }
}
