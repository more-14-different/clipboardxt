namespace ClipboardManager;

/// <summary>中文 ASCII 检索展开方式。</summary>
public static class PinyinFilterModes
{
    public const string Traditional = "Traditional";
    public const string Xiaohe = "Xiaohe";
    public const int CurrentIndexVersion = 3;

    public static bool IsValid(string? mode) =>
        string.Equals(mode, Traditional, StringComparison.OrdinalIgnoreCase)
        || string.Equals(mode, Xiaohe, StringComparison.OrdinalIgnoreCase);

    public static string Normalize(string? mode)
    {
        if (string.Equals(mode, Xiaohe, StringComparison.OrdinalIgnoreCase)) return Xiaohe;
        return Traditional;
    }

    public static string DisplayName(string? mode) =>
        Normalize(mode) == Xiaohe ? "小鹤双拼" : "传统拼音";
}
