namespace ClipboardManager;

/// <summary>通过独立 ClipboardX 进程写入并短暂持有 Alt-V 文本剪贴板。</summary>
internal static partial class AltVClipboardProvider
{
    private const string ModeArg = "--altv-provider-settext";
    private const string RequestArg = "--request-file";
    private const string ResultArg = "--result-file";
    private const string StopArg = "--stop-file";
    private const int ProviderRetries = 18;
    private const int ProviderDelayMs = 75;
    private const int ProviderHoldTimeoutMs = 8_000;
    private const int ProviderResultTimeoutMs = 5_000;

    internal readonly record struct Result(bool Success, bool ClipboardLocked, string Error);
}
