using System.IO;
using System.Text.Json;
using ClipboardManager.Models;
using ClipboardManager.Services;
using Microsoft.Data.Sqlite;

namespace ClipboardManager;

internal sealed partial class ClipboardHistoryStore
{    public void DeleteAll()
    {
        try
        {
            using var conn = Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM clipboard_history WHERE COALESCE(shortcut_phrase, '') = ''";
            cmd.ExecuteNonQuery();
        }
        catch
        {
            // ignore
        }
    }
}

