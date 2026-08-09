using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Xml.Linq;

namespace ClipboardManager.Tests;

public class LocalizationCoverageTests
{
    private static readonly Regex CSharpChineseLiteral = new(
        "\\\"([^\\\"\\r\\n]*[\\p{IsCJKUnifiedIdeographs}][^\\\"\\r\\n]*)\\\"",
        RegexOptions.Compiled);

    [Theory]
    [InlineData("12 条结果")]
    [InlineData("3 个文件")]
    [InlineData("1920×1080 图片")]
    [InlineData("共 8 个文件 · 3 张图片")]
    [InlineData("按住面板主键 Ctrl，直接粘贴列表第 1～9 条。")]
    [InlineData("热键 Ctrl+` 注册失败，可能被其他程序占用")]
    [InlineData("快捷键 Ctrl+G（文件对话框跳转）注册失败，可能与其他软件冲突")]
    [InlineData("批量模式切换快捷键 Alt+/ 注册失败，已恢复原快捷键")]
    [InlineData("识别图片中的文字需要 Windows 的「English (United States)」OCR 组件（约几十 MB，从 Windows 更新下载）。")]
    public void DynamicUiText_HasCompleteEnglishTranslation(string source)
    {
        var translated = EnglishTranslations.Translate(source, english: true);

        Assert.False(EnglishTranslations.ContainsHan(translated), translated);
    }

    [Fact]
    public void EveryChineseXamlUiValue_HasEnglishTranslation()
    {
        var root = FindRepositoryRoot();
        var values = Directory.EnumerateFiles(Path.Combine(root, "Views"), "*.xaml")
            .Select(XDocument.Load)
            .SelectMany(document => document.DescendantNodes().OfType<XText>()
                .Select(text => text.Value.Trim())
                .Concat(document.Descendants().Attributes().Select(attribute => attribute.Value.Trim())))
            .Where(EnglishTranslations.ContainsHan)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var untranslated = values
            .Where(source => EnglishTranslations.ContainsHan(
                EnglishTranslations.Translate(source, english: true)))
            .ToList();

        Assert.True(untranslated.Count == 0,
            "Missing XAML translations:\n" + string.Join("\n", untranslated));
    }

    [Fact]
    public void RuntimeGeneratedUiLiterals_HaveEnglishTranslations()
    {
        var root = FindRepositoryRoot();
        var views = Path.Combine(root, "Views");
        var filePatterns = new[]
        {
            "SettingsWindow*.cs",
            "PopupWindow.FooterHints.cs",
            "PopupWindow.SearchFilter.cs",
            "PopupWindow.FilterCommands.cs",
            "PopupWindow.ContextMenu.Sync.cs",
            "PopupWindow.EntryPreviewContent.cs",
            "PopupWindow.EntryTextEdit.cs",
            "PopupWindow.Pin.cs",
            "PopupWindow.QuickPastePhraseEditor.cs",
            "FileDialogJumpPickerWindow.PreviewFooter.cs",
            "FileDialogJumpPickerWindow.Search.Filter.cs",
            "FileDialogJumpPickerWindow.ListInteraction.Favorites.cs",
            "ExplorerQuickFindWindow*.cs",
            "OcrInstallPromptWindow.xaml.cs",
            "ProcessPickerDialog.cs"
        };

        var files = filePatterns
            .SelectMany(pattern => Directory.EnumerateFiles(views, pattern))
            .Distinct(StringComparer.OrdinalIgnoreCase);
        var literals = files.SelectMany(file => File.ReadLines(file)
                .Where(line => !line.TrimStart().StartsWith("//", StringComparison.Ordinal))
                .SelectMany(line => CSharpChineseLiteral.Matches(line).Select(match =>
                    match.Groups[1].Value.Replace("\\n", "\n", StringComparison.Ordinal))))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var untranslated = literals
            .Where(source => EnglishTranslations.ContainsHan(
                EnglishTranslations.Translate(source, english: true)))
            .ToList();

        Assert.True(untranslated.Count == 0,
            "Missing runtime UI translations:\n" + string.Join("\n", untranslated));
    }

    [Fact]
    public void AppDialogsTrayAndInstallerLiterals_HaveEnglishTranslations()
    {
        var root = FindRepositoryRoot();
        var files = Directory.EnumerateFiles(root, "App.*.cs")
            .Concat(Directory.EnumerateFiles(Path.Combine(root, "Install"), "*.cs"));
        var literals = files.SelectMany(file => File.ReadLines(file)
                .Where(line => !line.TrimStart().StartsWith("//", StringComparison.Ordinal))
                .Where(line => !line.Contains("Console.WriteLine", StringComparison.Ordinal))
                .SelectMany(line => CSharpChineseLiteral.Matches(line).Select(match =>
                    match.Groups[1].Value
                        .Replace("\\n", "\n", StringComparison.Ordinal)
                        .Replace("\\\\", "\\", StringComparison.Ordinal))))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var untranslated = literals
            .Where(source => EnglishTranslations.ContainsHan(
                EnglishTranslations.Translate(source, english: true)))
            .ToList();

        Assert.True(untranslated.Count == 0,
            "Missing app-shell translations:\n" + string.Join("\n", untranslated));
    }

    [Fact]
    public void UiLanguage_IsCopiedAndAppliedWithEditableSettings()
    {
        var source = new AppSettings { UiLanguage = UiLanguage.English };
        var copy = source.ShallowCopy();
        var target = new AppSettings();

        target.ApplyEditableSettingsFrom(copy);

        Assert.Equal(UiLanguage.English, copy.UiLanguage);
        Assert.Equal(UiLanguage.English, target.UiLanguage);
    }

    [Fact]
    public void LoadedWpfText_TranslatesUpdatesAndRestoresAtRuntime()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                UiLanguage.Initialize(UiLanguage.English);
                var text = new TextBlock { Text = "设置" };
                text.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent, text));
                Assert.Equal("Settings", text.Text);

                text.Text = "无匹配结果";
                Assert.Equal("No matching results", text.Text);

                UiLanguage.Set(UiLanguage.Chinese);
                text.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent, text));
                Assert.Equal("无匹配结果", text.Text);
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        Assert.Null(failure);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "clipboardx.sln")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
    }
}
