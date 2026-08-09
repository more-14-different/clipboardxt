using System.IO;
using System.Text.Json;
using ClipboardManager.Models;
using ClipboardManager.Services;
using Microsoft.Data.Sqlite;

namespace ClipboardManager;

/// <summary>
/// 剪贴板普通历史（不含快捷短语）的 SQLite 持久化。库路径：%LocalAppData%\ClipboardX\clipboard_history.db
/// </summary>
internal sealed partial class ClipboardHistoryStore
{
    private const int ArchiveBucketMaxItems = 100_000;
    public static string PinyinFilterMode { get; set; } = PinyinFilterModes.Traditional;

    private readonly string _dbPath;

    private string ConnectionString => new SqliteConnectionStringBuilder
    {
        DataSource = _dbPath,
        Mode = SqliteOpenMode.ReadWriteCreate,
        Cache = SqliteCacheMode.Shared
    }.ToString();

    public ClipboardHistoryStore() : this(AppPaths.SqliteDbFile)
    {
    }

    internal ClipboardHistoryStore(string dbPath)
    {
        _dbPath = Path.GetFullPath(dbPath);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_dbPath)!);
            EnsureSchema();
        }
        catch
        {
            // 降级为仅内存历史
        }
    }

}
