using ClipboardManager;

namespace ClipboardX.Tests;

public sealed class FileDialogJumpPickerSearchPasteTests
{
    [Theory]
    [InlineData("alpha\r\nbeta", "alpha beta")]
    [InlineData("alpha\rbeta\ngamma\tdelta", "alpha beta gamma delta")]
    [InlineData(@"D:\Work Folder", @"D:\Work Folder")]
    public void NormalizePastedSearchText_FlattensMultilineText(
        string input,
        string expected)
    {
        Assert.Equal(expected, SearchEditorText.NormalizePastedText(input));
    }

    [Theory]
    [InlineData(' ', "\u00A0")]
    [InlineData('x', "x")]
    public void ToDisplayCharacter_PreservesVisibleWidth(char character, string expected)
    {
        Assert.Equal(expected, SearchEditorText.ToDisplayCharacter(character));
    }
}
