using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using ClipboardManager.Models;

namespace ClipboardManager;

public partial class PopupWindow : Window
{
    private FileDialogJumpPickerWindow? _fileJumpSearchPasteTarget;
    private volatile bool _isFileJumpSearchPasteRoutingActive;

    private void BeginFileJumpSearchPasteRoutingIfAvailable()
    {
        EndFileJumpSearchPasteRouting();
        var picker = _activeFileJumpPicker;
        if (picker?.CanReceiveSearchPaste != true) return;

        _fileJumpSearchPasteTarget = picker;
        _isFileJumpSearchPasteRoutingActive = true;
        picker.SetClipboardPopupInteraction(true);
    }

    private void EndFileJumpSearchPasteRouting()
    {
        var picker = _fileJumpSearchPasteTarget;
        _fileJumpSearchPasteTarget = null;
        _isFileJumpSearchPasteRoutingActive = false;
        picker?.SetClipboardPopupInteraction(false);
    }

    private Task<bool> TryPasteEntryIntoFileJumpSearchAsync(
        ClipboardEntry item,
        bool hidePopupAfter)
    {
        if (!_isFileJumpSearchPasteRoutingActive) return Task.FromResult(false);

        return PasteTextIntoFileJumpSearchAsync(
            item.Type == EntryType.Text ? item.TextContent : null,
            hidePopupAfter);
    }

    private Task<bool> TryPasteEntriesIntoFileJumpSearchAsync(
        IReadOnlyList<ClipboardEntry> entries,
        bool newlineAfterEachText)
    {
        if (!_isFileJumpSearchPasteRoutingActive) return Task.FromResult(false);

        var text = new StringBuilder();
        foreach (var entry in entries)
        {
            if (entry.Type != EntryType.Text) continue;
            text.Append(entry.TextContent);
            if (newlineAfterEachText)
                text.Append(Environment.NewLine);
        }

        return PasteTextIntoFileJumpSearchAsync(text.ToString(), hidePopupAfter: true);
    }

    private Task<bool> PasteTextIntoFileJumpSearchAsync(
        string? text,
        bool hidePopupAfter)
    {
        if (!_isFileJumpSearchPasteRoutingActive) return Task.FromResult(false);

        var picker = _fileJumpSearchPasteTarget;
        // 文件夹筛选只接受文本。图片和文件条目在此消费掉，避免穿透粘贴到宿主窗口。
        // 文本已在 ClipboardX 历史项内，直接写搜索状态；不要重新打开系统剪贴板，
        // 否则 uTools/远程剪贴板等持锁时，SetText 的每次尝试可能同步阻塞约 1 秒。
        if (picker?.CanReceiveSearchPaste == true && !string.IsNullOrEmpty(text))
        {
            picker.InsertPastedSearchText(text);
            ClipboardDiagnosticsLog.Write($"filejump search paste direct len={text.Length}");
        }

        if (hidePopupAfter)
            HidePopup();

        return Task.FromResult(true);
    }
}
