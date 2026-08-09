namespace ClipboardManager;

public sealed record SettingsSaveValidationInput(
    string MaxItemsText,
    string PreviewLinesText,
    string PopupWidthText,
    string PopupMaxHeightText,
    string PopupPageItemsText,
    string FileJumpDelayMsText,
    string RecentFolderMaxCountText,
    uint HotkeyModifiers,
    uint HotkeyKey,
    uint FileJumpHotkeyModifiers,
    uint FileJumpHotkeyKey,
    uint BatchModeCycleHotkeyModifiers,
    uint BatchModeCycleHotkeyKey,
    uint PageScrollUpModifiers,
    uint PageScrollUpKey,
    uint PageScrollDownModifiers,
    uint PageScrollDownKey,
    string? ExplorerEverythingMaxResultsText = null);

public sealed record SettingsSaveValidationValues(
    int MaxItems,
    int PreviewLines,
    double PopupWidth,
    double PopupMaxHeight,
    int PopupPageItems,
    int FileJumpDelayMs,
    int RecentFolderMaxCount,
    int? ExplorerEverythingMaxResults);

public sealed record SettingsSaveValidationResult(
    bool IsValid,
    SettingsSaveValidationValues? Values,
    string? Message)
{
    public static SettingsSaveValidationResult Success(SettingsSaveValidationValues values) =>
        new(true, values, null);

    public static SettingsSaveValidationResult Failure(string message) =>
        new(false, null, message);
}

public static class SettingsSaveValidator
{
    public static SettingsSaveValidationResult Validate(SettingsSaveValidationInput input)
    {
        if (!TryParseRange(input.MaxItemsText, 10, 100000, out var maxItems))
            return SettingsSaveValidationResult.Failure("最大记录数应在 10 ~ 100000 之间");

        if (!TryParseRange(input.PreviewLinesText, 1, 10, out var previewLines))
            return SettingsSaveValidationResult.Failure("预览行数应在 1 ~ 10 之间");

        if (!TryParseRange(input.PopupWidthText, 280, 1200, out var popupW))
            return SettingsSaveValidationResult.Failure("面板宽度应在 280 ~ 1200（像素）之间");

        if (!TryParseRange(input.PopupMaxHeightText, 200, 900, out var popupH))
            return SettingsSaveValidationResult.Failure("面板最大高度应在 200 ~ 900（像素）之间");

        if (!TryParseRange(input.PopupPageItemsText, 1, 50, out var pageItems))
            return SettingsSaveValidationResult.Failure("每次翻页条数应在 1 ~ 50 之间");

        if (SameHotkey(input.PageScrollUpModifiers, input.PageScrollUpKey, input.PageScrollDownModifiers, input.PageScrollDownKey))
            return SettingsSaveValidationResult.Failure("向上翻页与向下翻页快捷键不能相同。");

        if (!TryParseRange(input.FileJumpDelayMsText, 0, 10000, out var jumpDelayMs))
            return SettingsSaveValidationResult.Failure("跳转列表延时应在 0 ~ 10000 毫秒之间（0 表示立即弹出）");

        if (SameHotkey(input.HotkeyModifiers, input.HotkeyKey, input.FileJumpHotkeyModifiers, input.FileJumpHotkeyKey))
            return SettingsSaveValidationResult.Failure("呼出快捷键与文件对话框跳转键不能相同。");

        if (SameHotkey(input.HotkeyModifiers, input.HotkeyKey, input.BatchModeCycleHotkeyModifiers, input.BatchModeCycleHotkeyKey))
            return SettingsSaveValidationResult.Failure("呼出快捷键与批量模式切换键不能相同。");

        if (SameHotkey(input.FileJumpHotkeyModifiers, input.FileJumpHotkeyKey, input.BatchModeCycleHotkeyModifiers, input.BatchModeCycleHotkeyKey))
            return SettingsSaveValidationResult.Failure("文件对话框跳转键与批量模式切换键不能相同。");

        int? explorerEverythingMaxResults = null;
        if (input.ExplorerEverythingMaxResultsText != null)
        {
            if (!TryParseRange(input.ExplorerEverythingMaxResultsText, 1, 2000, out var explorerEvMax))
                return SettingsSaveValidationResult.Failure("筛选最大条数应在 1 ~ 2000 之间");
            explorerEverythingMaxResults = explorerEvMax;
        }

        if (!TryParseRange(input.RecentFolderMaxCountText, 1, 50, out var recentMaxCount))
            return SettingsSaveValidationResult.Failure("常用路径最大数量应在 1 ~ 50 之间");

        return SettingsSaveValidationResult.Success(new SettingsSaveValidationValues(
            maxItems,
            previewLines,
            popupW,
            popupH,
            pageItems,
            jumpDelayMs,
            recentMaxCount,
            explorerEverythingMaxResults));
    }

    private static bool TryParseRange(string text, int min, int max, out int value) =>
        int.TryParse(text, out value) && value >= min && value <= max;

    private static bool TryParseRange(string text, double min, double max, out double value) =>
        double.TryParse(text, out value) && value >= min && value <= max;

    private static bool SameHotkey(uint leftModifiers, uint leftKey, uint rightModifiers, uint rightKey) =>
        leftModifiers == rightModifiers && leftKey == rightKey;
}
