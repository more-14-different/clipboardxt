namespace ClipboardManager.Tests;

public sealed class SearchEditorSelectionTests
{
    [Theory]
    [InlineData("alpha beta", 2, 0, 5)]
    [InlineData("alpha beta", 6, 6, 10)]
    [InlineData("alpha  beta", 5, 5, 7)]
    [InlineData(@"C:\Users\name", 2, 1, 3)]
    [InlineData("中文检索 test", 1, 0, 4)]
    public void TryGetUnitRange_SelectsContiguousCharacterKind(
        string text,
        int characterIndex,
        int expectedStart,
        int expectedEnd)
    {
        var found = SearchEditorSelection.TryGetUnitRange(text, characterIndex, out var range);

        Assert.True(found);
        Assert.Equal(new SearchEditorSelection.Range(expectedStart, expectedEnd), range);
    }

    [Theory]
    [InlineData("", 0)]
    [InlineData("abc", -1)]
    [InlineData("abc", 3)]
    public void TryGetUnitRange_RejectsPositionsOutsideText(string text, int characterIndex)
    {
        Assert.False(SearchEditorSelection.TryGetUnitRange(text, characterIndex, out _));
    }
}
