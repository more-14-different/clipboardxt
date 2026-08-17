using System.Text;

namespace ClipboardManager.Models;

public sealed class ClipboardSourceInfo
{
    public string? AppName { get; set; }
    public string? ExeName { get; set; }
    public string? ExePath { get; set; }
    public string? WindowTitle { get; set; }
    public string? WindowClass { get; set; }
    public string? FocusedClass { get; set; }
    public uint ProcessId { get; set; }
    public long Hwnd { get; set; }
    public string? CaptureMethod { get; set; }

    public bool HasAny =>
        !string.IsNullOrWhiteSpace(AppName)
        || !string.IsNullOrWhiteSpace(ExeName)
        || !string.IsNullOrWhiteSpace(WindowTitle)
        || !string.IsNullOrWhiteSpace(WindowClass)
        || !string.IsNullOrWhiteSpace(FocusedClass)
        || !string.IsNullOrWhiteSpace(ExePath)
        || ProcessId != 0
        || Hwnd != 0
        || !string.IsNullOrWhiteSpace(CaptureMethod);

    public string DisplayName
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(AppName)) return AppName!;
            if (!string.IsNullOrWhiteSpace(ExeName)) return ExeName!;
            return "";
        }
    }

    public string BuildSearchText()
    {
        var sb = new StringBuilder();
        Append(sb, AppName);
        Append(sb, ExeName);
        Append(sb, ExePath);
        Append(sb, WindowTitle);
        Append(sb, WindowClass);
        Append(sb, FocusedClass);
        if (ProcessId != 0) Append(sb, ProcessId.ToString());
        if (Hwnd != 0) Append(sb, Hwnd.ToString());
        Append(sb, CaptureMethod);
        return sb.ToString();
    }

    public ClipboardSourceInfo Clone() => new()
    {
        AppName = AppName,
        ExeName = ExeName,
        ExePath = ExePath,
        WindowTitle = WindowTitle,
        WindowClass = WindowClass,
        FocusedClass = FocusedClass,
        ProcessId = ProcessId,
        Hwnd = Hwnd,
        CaptureMethod = CaptureMethod
    };

    private static void Append(StringBuilder sb, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        if (sb.Length > 0) sb.Append(' ');
        sb.Append(value.Trim());
    }
}
