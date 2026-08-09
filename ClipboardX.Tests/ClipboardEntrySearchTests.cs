using ClipboardManager;
using ClipboardManager.Models;

namespace ClipboardX.Tests;

public sealed class ClipboardEntrySearchTests
{
    [Theory]
    [InlineData("d tauri ")]
    [InlineData(" d tauri ")]
    public void MatchesSearch_FileEntryUsesFullPath(string query)
    {
        var entry = new ClipboardEntry
        {
            Type = EntryType.Files,
            FilePaths = [@"D:\C2D\Desktop\Code\Rust\sTools\komorebi-shortcuts-tauri"],
        };

        Assert.True(entry.MatchesSearch(query));
    }

    [Theory]
    [InlineData("d tauri ")]
    [InlineData(" d tauri ")]
    public void MatchesSearch_TextEntryAnchorsAgainstContentBeforeSourceMetadata(string query)
    {
        var entry = new ClipboardEntry
        {
            Type = EntryType.Text,
            TextContent = @"D:\C2D\Desktop\Code\Rust\sTools\komorebi-shortcuts-tauri",
            Source = new ClipboardSourceInfo { AppName = "Code", WindowTitle = "notes after text" },
        };

        Assert.True(entry.MatchesSearch(query));
    }

    [Fact]
    public void MatchesSearch_FindsShortcutPhrase()
    {
        var entry = new ClipboardEntry
        {
            Type = EntryType.Text,
            TextContent = "unrelated content",
            ShortcutPhrase = "deploy-production",
        };

        Assert.True(entry.MatchesSearch("deploy-production"));
    }

    [Theory]
    [InlineData("快捷短语")]
    [InlineData("标题")]
    [InlineData("路径")]
    [InlineData("进程")]
    [InlineData("窗口")]
    [InlineData("焦点")]
    [InlineData("方式")]
    public void MatchesSearch_DoesNotIndexInfoFieldLabels(string query)
    {
        var entry = new ClipboardEntry
        {
            Type = EntryType.Text,
            TextContent = "unrelated content",
            ShortcutPhrase = "deploy-production",
            Source = new ClipboardSourceInfo
            {
                AppName = "Code",
                ExeName = "code.exe",
                ExePath = @"C:\Apps\Code\code.exe",
                WindowTitle = "notes",
                WindowClass = "Chrome_WidgetWin",
                FocusedClass = "Edit",
                CaptureMethod = "uia",
            },
        };

        Assert.False(entry.MatchesSearch(query));
    }
}
