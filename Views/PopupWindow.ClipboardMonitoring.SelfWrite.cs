using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Brush = System.Windows.Media.Brush;
using Button = System.Windows.Controls.Button;
using Orientation = System.Windows.Controls.Orientation;
using TextBox = System.Windows.Controls.TextBox;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using ClipboardManager.Models;

namespace ClipboardManager;

public partial class PopupWindow : Window
{
    /// <summary>记录一次自写后的剪贴板序列号与时间戳，供 OnClipboardUpdate 回波识别兜底。</summary>
    private void MarkSelfWroteClipboard()
    {
        _lastSelfWriteClipboardSeq = Win32.GetClipboardSequenceNumber();
        _lastSelfWriteTickMs = Environment.TickCount64;
    }

    /// <summary>批量粘贴的合并产物（合并字符串 / 合并 FileDropList）作为新条目入库，与系统剪贴板保持一致。
    /// 必须在 UI 线程调用；调用方需保证 OnClipboardUpdate 路径已被 <see cref="MarkSelfWroteClipboard"/> 拦截，避免重复入库。</summary>
    private void InsertBatchMergedEntry(ClipboardEntry entry)
    {
        if (entry.Type == EntryType.Text)
        {
            if (string.IsNullOrEmpty(entry.TextContent)) return;
            DeduplicateText(entry.TextContent);
        }
        else if (entry.Type == EntryType.Files)
        {
            if (entry.FilePaths is null || entry.FilePaths.Length == 0) return;
            DeduplicateFiles(entry.FilePaths);
        }
        else
        {
            return;
        }
        _allItems.Insert(0, entry);
        TrimItems();
        _historyStore.TryInsert(entry);
        RefreshFilter();
    }
}
