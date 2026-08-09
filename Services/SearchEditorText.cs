namespace ClipboardManager;

internal static class SearchEditorText
{
    public const int MaxLength = 4096;

    public static string NormalizePastedText(string text) =>
        text.Replace("\r\n", " ", StringComparison.Ordinal)
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Replace('\t', ' ');

    public static string ToDisplayCharacter(char character) =>
        character == ' ' ? "\u00A0" : character.ToString();

    public static int FindUnitLeft(string text, int index)
    {
        var i = Math.Clamp(index, 0, text.Length);
        while (i > 0 && char.IsWhiteSpace(text[i - 1])) i--;
        if (i <= 0) return 0;

        var kind = GetCharacterKind(text[i - 1]);
        while (i > 0 && GetCharacterKind(text[i - 1]) == kind) i--;
        return i;
    }

    public static int FindUnitRight(string text, int index)
    {
        var i = Math.Clamp(index, 0, text.Length);
        while (i < text.Length && char.IsWhiteSpace(text[i])) i++;
        if (i >= text.Length) return text.Length;

        var kind = GetCharacterKind(text[i]);
        while (i < text.Length && GetCharacterKind(text[i]) == kind) i++;
        while (i < text.Length && char.IsWhiteSpace(text[i])) i++;
        return i;
    }

    private static CharacterKind GetCharacterKind(char character) =>
        char.IsLetterOrDigit(character) || character == '_'
            ? CharacterKind.Word
            : CharacterKind.Symbol;

    private enum CharacterKind
    {
        Word,
        Symbol
    }
}
