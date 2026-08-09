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
    private static readonly string[] VolatileSourceAppNeedles =
    [
        "autohotkey",
        "keyhac",
        "utools",
        "keypirinha",
        "flow.launcher",
        "flowlauncher",
        "listary",
        "wox",
        "ueli",
        "executor",
        "launchy",
        "powertoys",
        "espanso",
        "textexpander",
        "phraseexpress",
        "ditto",
        "copyq",
        "python",
        "pythonw",
        "pwsh",
        "powershell",
        "cmd",
        "wscript",
        "cscript"
    ];

    private void PreserveDuplicateSourceIfNeeded(ClipboardEntry incoming, Func<ClipboardEntry, bool> duplicatePredicate)
    {
        var existing = _allItems.FirstOrDefault(x => duplicatePredicate(x));
        if (existing?.Source == null || !existing.Source.HasAny) return;

        var incomingSource = incoming.Source;
        var shouldPreserve = incomingSource == null
            || !incomingSource.HasAny
            || IsVolatileSource(incomingSource)
            || Math.Abs((incoming.CopiedAt - existing.CopiedAt).TotalMilliseconds) <= 1500;

        if (!shouldPreserve) return;

        incoming.Source = existing.Source.Clone();
        incoming.SourceIconPng = existing.SourceIconPng;
        ClipboardDiagnosticsLog.Write(
            $"monitor preserve duplicate source type={incoming.Type} oldApp={existing.Source.DisplayName} newApp={incomingSource?.DisplayName}");
    }

    private static bool IsVolatileSource(ClipboardManager.Models.ClipboardSourceInfo source)
    {
        var haystack = string.Join(" ",
            source.AppName,
            source.ExeName,
            source.ExePath,
            source.WindowTitle,
            source.WindowClass,
            source.FocusedClass);
        if (string.IsNullOrWhiteSpace(haystack)) return true;

        foreach (var needle in VolatileSourceAppNeedles)
        {
            if (haystack.Contains(needle, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private void DeduplicateText(string text)
    {
        foreach (var x in _allItems.Where(x => x.Type == EntryType.Text && !x.IsQuickPaste && x.TextContent == text))
            _historyStore.TryDelete(x.PersistedId);
        _allItems.RemoveAll(x => x.Type == EntryType.Text && !x.IsQuickPaste && x.TextContent == text);
        // 一次复制常连发多条 WM_CLIPBOARDUPDATE；旧条已从列表移除，若不清理队列会叠多条同内容角标
        var removedFromQ = _batchQueue.RemoveAll(x => x.Type == EntryType.Text && !x.IsQuickPaste && x.TextContent == text);
#if CLIPX_CLIPBOARD
        if (removedFromQ > 0)
            RequestBatchQueueHeadClipboardResyncAfterDedup();
#endif
    }

    private void DeduplicateFiles(string[] paths)
    {
        var key = string.Join("|", paths);
        foreach (var x in _allItems.Where(x => x.Type == EntryType.Files && string.Join("|", x.FilePaths ?? []) == key))
            _historyStore.TryDelete(x.PersistedId);
        _allItems.RemoveAll(x => x.Type == EntryType.Files && string.Join("|", x.FilePaths ?? []) == key);
        var removedFromQ = _batchQueue.RemoveAll(x => x.Type == EntryType.Files && string.Join("|", x.FilePaths ?? []) == key);
#if CLIPX_CLIPBOARD
        if (removedFromQ > 0)
            RequestBatchQueueHeadClipboardResyncAfterDedup();
#endif
    }

    /// <summary>按 PNG 字节 MD5 去掉已有相同图片（含本程序粘贴触发的重复监控写入）。</summary>
    private void DeduplicateImageByMd5(byte[] pngData)
    {
        if (pngData == null || pngData.Length == 0) return;
        var hex = ClipboardEntry.ComputeImageBytesMd5Hex(pngData);
        if (hex.Length == 0) return;
        foreach (var x in _allItems.Where(x => x.Type == EntryType.Image && !x.IsQuickPaste && x.ImageContentMd5Hex == hex))
            _historyStore.TryDelete(x.PersistedId);
        _allItems.RemoveAll(x => x.Type == EntryType.Image && !x.IsQuickPaste && x.ImageContentMd5Hex == hex);
        var removedFromQ = _batchQueue.RemoveAll(x => x.Type == EntryType.Image && !x.IsQuickPaste && x.ImageContentMd5Hex == hex);
#if CLIPX_CLIPBOARD
        if (removedFromQ > 0)
            RequestBatchQueueHeadClipboardResyncAfterDedup();
#endif
    }

    private void TrimItems()
    {
        var regular = _allItems.Where(x => !x.IsQuickPaste).ToList();
#if CLIPX_CLIPBOARD
        var queueTouched = false;
#endif
        while (regular.Count > _maxItems)
        {
            var last = regular[^1];
            _historyStore.TryArchiveAndDelete(last.PersistedId);
            _allItems.Remove(last);
#if CLIPX_CLIPBOARD
            if (_batchQueue.Remove(last))
                queueTouched = true;
#else
            _batchQueue.Remove(last);
#endif
            regular.RemoveAt(regular.Count - 1);
        }

        var maxImages = _appSettings?.MaxImageItems ?? 150;
        if (maxImages >= 0)
        {
            var excessImages = regular.Where(x => x.Type == EntryType.Image && !x.IsStarred)
                .Skip(maxImages)
                .ToList();
            foreach (var image in excessImages)
            {
                _historyStore.TryDelete(image.PersistedId);
                _allItems.Remove(image);
                _batchQueue.Remove(image);
            }
        }
#if CLIPX_CLIPBOARD
        if (queueTouched)
            RequestBatchQueueHeadClipboardResyncAfterDedup();
#endif
    }
}
