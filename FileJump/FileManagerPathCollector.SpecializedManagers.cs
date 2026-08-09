using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace ClipboardManager;

/// <summary>Total Commander、XYplorer 与 Directory Opus 的专用路径查询协议。</summary>
internal static partial class FileManagerPathCollector
{
    private const int TcMsg = 1075;
    private const int TcmCopySrcPathToClip = 2029;
    private const int TcmCopyTrgPathToClip = 2030;
    private const nint XyCopyDataId = 0x400001;

    private static bool TryTotalCommanderPathFromClip(IntPtr tcHwnd, int commandId, out string path)
    {
        path = "";
        ClipboardGate.Enter();
        try
        {
            string? backup = null;
            try
            {
                if (System.Windows.Clipboard.ContainsText()) backup = System.Windows.Clipboard.GetText();
            }
            catch { /* ignore */ }

            try { System.Windows.Clipboard.Clear(); } catch { /* ignore */ }

            Win32.SendMessage(tcHwnd, TcMsg, (IntPtr)commandId, IntPtr.Zero);
            Thread.Sleep(90);
            try
            {
                path = System.Windows.Clipboard.GetText()?.Trim() ?? "";
            }
            catch { path = ""; }

            try
            {
                if (backup != null) System.Windows.Clipboard.SetText(backup);
                else System.Windows.Clipboard.Clear();
            }
            catch { /* ignore */ }

            return Directory.Exists(path);
        }
        finally
        {
            ClipboardGate.Exit();
        }
    }

    private static bool TryXyplorerPathFromClip(IntPtr xyHwnd, string script, out string path)
    {
        path = "";
        ClipboardGate.Enter();
        try
        {
            SendXyplorerCopyData(xyHwnd, script);
            Thread.Sleep(120);
            try
            {
                path = System.Windows.Clipboard.GetText()?.Trim() ?? "";
            }
            catch { path = ""; }

            return Directory.Exists(path);
        }
        finally
        {
            ClipboardGate.Exit();
        }
    }

    private static void SendXyplorerCopyData(IntPtr xyHwnd, string message)
    {
        var bytes = Encoding.Unicode.GetBytes(message);
        var ptr = Marshal.AllocHGlobal(bytes.Length);
        try
        {
            Marshal.Copy(bytes, 0, ptr, bytes.Length);
            var cds = new Win32.COPYDATASTRUCT
            {
                dwData = (IntPtr)XyCopyDataId,
                cbData = bytes.Length,
                lpData = ptr
            };
            Win32.SendMessage(xyHwnd, Win32.WM_COPYDATA, IntPtr.Zero, ref cds);
        }
        finally
        {
            Marshal.FreeHGlobal(ptr);
        }
    }

    private static string? TryRunDopusInfoXml(IntPtr anyListerHwnd)
    {
        if (!TryGetProcessImagePath(anyListerHwnd, out var exe)) return null;
        var dir = Path.GetDirectoryName(exe);
        if (string.IsNullOrEmpty(dir)) return null;
        var rt = Path.Combine(dir, "dopusrt.exe");
        if (!File.Exists(rt)) return null;

        var temp = Path.Combine(Path.GetTempPath(), "ClipboardX-dopus-" + Guid.NewGuid().ToString("N") + ".xml");
        try
        {
            using var p = Process.Start(new ProcessStartInfo
            {
                FileName = rt,
                ArgumentList = { "/info", temp, "paths" },
                UseShellExecute = false,
                CreateNoWindow = true,
            });
            p?.WaitForExit(5000);
            if (!File.Exists(temp)) return null;
            return File.ReadAllText(temp);
        }
        catch { return null; }
        finally
        {
            try { File.Delete(temp); } catch { /* ignore */ }
        }
    }

    internal static IEnumerable<(string label, string path)> ParseDopusListerPaths(string? xml, IntPtr listerHwnd)
    {
        if (string.IsNullOrEmpty(xml)) yield break;
        var idVariants = new HashSet<string>(StringComparer.Ordinal)
        {
            ((nint)listerHwnd).ToString(),
            unchecked((uint)(nint)listerHwnd).ToString(),
        };
        foreach (var (state, label) in new[] { ("1", "Directory Opus (活动)"), ("2", "Directory Opus (被动)") })
        {
            foreach (var id in idVariants)
            {
                var pattern =
                    $@"(?is)lister\s*=\s*""{Regex.Escape(id)}""[^>]*tab_state\s*=\s*""{state}""[^>]*>\s*([^<]+?)\s*</path>";
                var m = Regex.Match(xml, pattern);
                if (m.Success && Directory.Exists(m.Groups[1].Value.Trim()))
                {
                    yield return (label, m.Groups[1].Value.Trim());
                    break;
                }
            }
        }
    }
}
