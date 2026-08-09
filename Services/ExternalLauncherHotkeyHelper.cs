namespace ClipboardManager;

/// <summary>
/// 外部无焦点 launcher 使用的私有功能键范围。ClipboardX 面板显示时必须放行，
/// 让后弹出的 launcher 能取得输入所有权；当前 komorebi-shortcuts-tauri 从该范围选择触发键。
/// </summary>
internal static class ExternalLauncherHotkeyHelper
{
    private const uint VkF13 = 0x7C;
    private const uint VkF18 = 0x81;

    internal static bool IsTriggerKey(uint virtualKey) =>
        virtualKey is >= VkF13 and <= VkF18;
}
