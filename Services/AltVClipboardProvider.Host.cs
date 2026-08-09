using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Threading;
using WinForms = System.Windows.Forms;

namespace ClipboardManager;

internal static partial class AltVClipboardProvider
{
    public static bool TryHandleCommandLine(string[] args, out int exitCode)
    {
        exitCode = 0;
        if (!IsProviderCommand(args))
            return false;

        if (!TryParseProviderArguments(args, out var providerArguments))
        {
            exitCode = 2;
            return true;
        }

        try
        {
            var text = File.ReadAllText(providerArguments.RequestFile, new UTF8Encoding(false));
            var result = TrySetClipboardText(text);
            WriteResult(providerArguments.ResultFile, result);
            if (result.Success)
            {
                ClipboardDiagnosticsLog.Write(
                    $"provider hold begin len={text.Length} stop=\"{providerArguments.StopFile}\"");
                HoldClipboardProviderSession(providerArguments.StopFile, ProviderHoldTimeoutMs);
                ClipboardDiagnosticsLog.Write($"provider hold end len={text.Length}");
            }
            exitCode = result.Success ? 0 : 1;
            return true;
        }
        catch (Exception ex)
        {
            WriteResult(
                providerArguments.ResultFile,
                new Result(false, false, $"{ex.GetType().Name}: {ex.Message}"));
            exitCode = 1;
            return true;
        }
    }

    private static Result TrySetClipboardText(string text)
    {
        Exception? last = null;
        for (var i = 0; i < ProviderRetries; i++)
        {
            try
            {
                Win32.CloseClipboard();
                var dataObject = new WinForms.DataObject();
                dataObject.SetData(WinForms.DataFormats.UnicodeText, true, text);
                WinForms.Clipboard.SetDataObject(dataObject, true, 1, ProviderDelayMs);
                ClipboardDiagnosticsLog.Write(
                    $"provider SetDataObject ok len={text.Length} attempt={i + 1}/{ProviderRetries}");
                return new Result(true, false, "");
            }
            catch (Exception ex)
            {
                last = ex;
                var locked = IsClipboardCantOpen(ex);
                var hresult = ex is COMException com
                    ? $" hr=0x{(uint)com.HResult:X8}"
                    : "";
                ClipboardDiagnosticsLog.Write(
                    $"provider SetDataObject fail attempt={i + 1}/{ProviderRetries} {ex.GetType().Name}: {ex.Message}{hresult}");
                if (locked)
                    ClipboardDiagnosticsLog.Write($"provider owner {DescribeOpenClipboardOwner()}");

                if (i >= ProviderRetries - 1)
                    break;

                PumpOnce();
                Thread.Sleep(ProviderDelayMs);
            }
        }

        return new Result(
            false,
            last is not null && IsClipboardCantOpen(last),
            last?.Message ?? "Unknown provider failure");
    }

    private static void HoldClipboardProviderSession(string stopFile, int timeoutMs)
    {
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.ElapsedMilliseconds < timeoutMs)
        {
            if (File.Exists(stopFile))
                return;

            PumpOnce();
            Thread.Sleep(15);
        }
    }

    private static void PumpOnce()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(
            DispatcherPriority.Background,
            new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }

    private static bool IsClipboardCantOpen(Exception exception) =>
        exception is COMException com && com.HResult == unchecked((int)0x800401D0);

    private static string DescribeOpenClipboardOwner()
    {
        var owner = Win32.GetOpenClipboardWindow();
        if (owner == IntPtr.Zero)
            return "owner=NONE";

        var title = Win32.GetWindowText(owner);
        var windowClass = Win32.GetWindowClassName(owner);
        _ = Win32.GetWindowThreadProcessId(owner, out var processId);
        var processName = "";
        if (processId != 0)
        {
            try
            {
                using var process = Process.GetProcessById((int)processId);
                processName = process.ProcessName;
            }
            catch
            {
                processName = "?";
            }
        }

        return $"owner=0x{owner.ToInt64():X} pid={processId} proc={processName} class={windowClass} title=\"{title}\"";
    }
}
