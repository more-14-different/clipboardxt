using ClipboardManager;

namespace ClipboardX.Tests;

public sealed class SettingsSaveValidatorTests
{
    [Fact]
    public void Validate_ValidInput_ReturnsParsedValues()
    {
        var result = SettingsSaveValidator.Validate(ValidInput());

        Assert.True(result.IsValid);
        Assert.NotNull(result.Values);
        Assert.Equal(2000, result.Values.MaxItems);
        Assert.Equal(2, result.Values.PreviewLines);
        Assert.Equal(420, result.Values.PopupWidth);
        Assert.Equal(560, result.Values.PopupMaxHeight);
        Assert.Equal(8, result.Values.PopupPageItems);
        Assert.Equal(0, result.Values.FileJumpDelayMs);
        Assert.Equal(5, result.Values.RecentFolderMaxCount);
        Assert.Equal(200, result.Values.ExplorerEverythingMaxResults);
    }

    [Theory]
    [InlineData("9", "最大记录数应在 10 ~ 100000 之间")]
    [InlineData("100001", "最大记录数应在 10 ~ 100000 之间")]
    [InlineData("abc", "最大记录数应在 10 ~ 100000 之间")]
    public void Validate_MaxItemsOutOfRange_ReturnsMessage(string maxItems, string message)
    {
        var result = SettingsSaveValidator.Validate(ValidInput(maxItemsText: maxItems));

        Assert.False(result.IsValid);
        Assert.Equal(message, result.Message);
    }

    [Fact]
    public void Validate_PageScrollHotkeyConflict_ReturnsMessage()
    {
        var result = SettingsSaveValidator.Validate(ValidInput(
            pageScrollUpModifiers: Win32.MOD_CONTROL,
            pageScrollUpKey: 0xBD,
            pageScrollDownModifiers: Win32.MOD_CONTROL,
            pageScrollDownKey: 0xBD));

        Assert.False(result.IsValid);
        Assert.Equal("向上翻页与向下翻页快捷键不能相同。", result.Message);
    }

    [Fact]
    public void Validate_GlobalHotkeyConflict_ReturnsMessage()
    {
        var result = SettingsSaveValidator.Validate(ValidInput(
            hotkeyModifiers: Win32.MOD_CONTROL,
            hotkeyKey: Win32.VK_G,
            fileJumpHotkeyModifiers: Win32.MOD_CONTROL,
            fileJumpHotkeyKey: Win32.VK_G));

        Assert.False(result.IsValid);
        Assert.Equal("呼出快捷键与文件对话框跳转键不能相同。", result.Message);
    }

    [Fact]
    public void Validate_ExplorerMaxResultsWhenProvided_ReturnsMessage()
    {
        var result = SettingsSaveValidator.Validate(ValidInput(explorerEverythingMaxResultsText: "2001"));

        Assert.False(result.IsValid);
        Assert.Equal("筛选最大条数应在 1 ~ 2000 之间", result.Message);
    }

    [Fact]
    public void Validate_ExplorerMaxResultsWhenOmitted_IsNotRequired()
    {
        var result = SettingsSaveValidator.Validate(ValidInput(explorerEverythingMaxResultsText: null));

        Assert.True(result.IsValid);
        Assert.Null(result.Values!.ExplorerEverythingMaxResults);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("51")]
    public void Validate_RecentFolderMaxCountOutOfRange_ReturnsMessage(string value)
    {
        var input = ValidInput() with { RecentFolderMaxCountText = value };

        var result = SettingsSaveValidator.Validate(input);

        Assert.False(result.IsValid);
        Assert.Equal("常用路径最大数量应在 1 ~ 50 之间", result.Message);
    }

    [Fact]
    public void Validate_RecentFolderMaxCountAcceptsUpperBound()
    {
        var input = ValidInput() with { RecentFolderMaxCountText = "50" };

        var result = SettingsSaveValidator.Validate(input);

        Assert.True(result.IsValid);
        Assert.Equal(50, result.Values!.RecentFolderMaxCount);
    }

    private static SettingsSaveValidationInput ValidInput(
        string maxItemsText = "2000",
        uint hotkeyModifiers = Win32.MOD_CONTROL,
        uint hotkeyKey = Win32.VK_OEM_3,
        uint fileJumpHotkeyModifiers = Win32.MOD_CONTROL,
        uint fileJumpHotkeyKey = Win32.VK_G,
        uint batchModeCycleHotkeyModifiers = Win32.MOD_ALT,
        uint batchModeCycleHotkeyKey = Win32.VK_OEM_2,
        uint pageScrollUpModifiers = Win32.MOD_CONTROL,
        uint pageScrollUpKey = 0xBD,
        uint pageScrollDownModifiers = Win32.MOD_CONTROL,
        uint pageScrollDownKey = 0xBB,
        string? explorerEverythingMaxResultsText = "200") =>
        new(
            maxItemsText,
            "2",
            "420",
            "560",
            "8",
            "0",
            "5",
            hotkeyModifiers,
            hotkeyKey,
            fileJumpHotkeyModifiers,
            fileJumpHotkeyKey,
            batchModeCycleHotkeyModifiers,
            batchModeCycleHotkeyKey,
            pageScrollUpModifiers,
            pageScrollUpKey,
            pageScrollDownModifiers,
            pageScrollDownKey,
            explorerEverythingMaxResultsText);
}
