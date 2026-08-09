namespace ClipboardManager;

internal static class SearchEditorSelection
{
    internal readonly record struct Range(int Start, int End);

    internal static bool TryGetUnitRange(string text, int characterIndex, out Range range)
    {
        if (characterIndex < 0 || characterIndex >= text.Length)
        {
            range = default;
            return false;
        }

        var start = characterIndex;
        var end = characterIndex + 1;
        var kind = GetCharacterKind(text[characterIndex]);

        while (start > 0 && GetCharacterKind(text[start - 1]) == kind)
            start--;
        while (end < text.Length && GetCharacterKind(text[end]) == kind)
            end++;

        range = new Range(start, end);
        return true;
    }

    private static CharacterKind GetCharacterKind(char ch)
    {
        if (char.IsWhiteSpace(ch)) return CharacterKind.Whitespace;
        return char.IsLetterOrDigit(ch) || ch == '_'
            ? CharacterKind.Word
            : CharacterKind.Symbol;
    }

    private enum CharacterKind
    {
        Word,
        Symbol,
        Whitespace
    }
}
