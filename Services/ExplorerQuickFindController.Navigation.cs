using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace ClipboardManager;

public sealed partial class ExplorerQuickFindController : IDisposable
{    private static void NavigateAndSelect(IntPtr explorerFrame, string targetFullPath, string openMode)
    {
        // DirectOpen 模式：找到前台文件对话框并导航到目标文件所在目录
        if (openMode == "DirectOpen")
        {
            try
            {
                var fg = Win32.GetForegroundWindow();
                if (fg != IntPtr.Zero && FileDialogJumpHelper.IsLikelyFileDialog(fg))
                {
                    var targetDir = Path.GetDirectoryName(targetFullPath);
                    if (!string.IsNullOrEmpty(targetDir))
                    {
                        FileDialogJumpHelper.TryNavigateToFolder(fg, targetDir);
                        return;
                    }
                }
            }
            catch { /* fallback to Explorer mode */ }
        }

        try
        {
            if (TryNavigateViaShellCom(explorerFrame, targetFullPath))
                return;
        }
        catch { /* fallback */ }

        try
        {
            RevealViaShellApi(targetFullPath);
        }
        catch { /* ignore */ }
    }

    /// <summary>通过 Shell.Application.Windows 后期绑定 COM 就地导航并选中。</summary>
    private static bool TryNavigateViaShellCom(IntPtr explorerFrame, string targetFullPath)
    {
        var targetDir = Path.GetDirectoryName(targetFullPath);
        var targetName = Path.GetFileName(targetFullPath);
        if (string.IsNullOrEmpty(targetDir) || string.IsNullOrEmpty(targetName))
            return false;

        Type? t = Type.GetTypeFromProgID("Shell.Application");
        if (t == null) return false;

        object? shell = null;
        object? windows = null;
        try
        {
            shell = Activator.CreateInstance(t);
            if (shell == null) return false;

            windows = shell.GetType().InvokeMember("Windows",
                BindingFlags.InvokeMethod, null, shell, null);
            if (windows == null)
            {
                try
                {
                    windows = shell.GetType().InvokeMember("Windows",
                        BindingFlags.GetProperty, null, shell, null);
                }
                catch { return false; }
            }
            if (windows == null) return false;

            var wt = windows.GetType();
            var count = Convert.ToInt32(
                wt.InvokeMember("Count", BindingFlags.GetProperty, null, windows, null));

            object? matchedWin = null;
            try
            {
                for (var i = 0; i < count; i++)
                {
                    object? win = null;
                    try
                    {
                        win = wt.InvokeMember("Item", BindingFlags.InvokeMethod, null, windows, new object[] { i });
                        if (win == null) continue;

                        var hwndObj = TryGetComHwnd(win);
                        if (!ExplorerFrameMatches(explorerFrame, hwndObj))
                        {
                            Marshal.ReleaseComObject(win);
                            continue;
                        }

                        matchedWin = win;
                        break;
                    }
                    catch
                    {
                        if (win != null) Marshal.ReleaseComObject(win);
                    }
                }

                if (matchedWin == null) return false;

                var currentPath = ReadShellWindowPath(matchedWin);
                var needNavigate = !string.Equals(
                    NormPath(currentPath), NormPath(targetDir), StringComparison.OrdinalIgnoreCase);

                if (needNavigate)
                {
                    matchedWin.GetType().InvokeMember("Navigate",
                        BindingFlags.InvokeMethod, null, matchedWin, new object[] { targetDir });
                    // 轮询等待 Explorer 完成导航（SSD 通常 < 100ms），比固定 Sleep(400) 更快
                    for (int poll = 0; poll < 10; poll++)
                    {
                        Thread.Sleep(50);
                        try
                        {
                            var afterPath = ReadShellWindowPath(matchedWin);
                            if (string.Equals(NormPath(afterPath), NormPath(targetDir), StringComparison.OrdinalIgnoreCase))
                                break;
                        }
                        catch { break; }
                    }
                }

                TrySelectItem(matchedWin, targetName);
                return true;
            }
            finally
            {
                if (matchedWin != null) Marshal.ReleaseComObject(matchedWin);
            }
        }
        catch { return false; }
        finally
        {
            if (windows != null) Marshal.ReleaseComObject(windows);
            if (shell != null) Marshal.ReleaseComObject(shell);
        }
    }

    private static void TrySelectItem(object shellWindow, string fileName)
    {
        const int SVSI_SELECT = 1;
        const int SVSI_DESELECTOTHERS = 4;
        const int SVSI_ENSUREVISIBLE = 8;
        const int SVSI_FOCUSED = 16;
        const int flags = SVSI_SELECT | SVSI_DESELECTOTHERS | SVSI_ENSUREVISIBLE | SVSI_FOCUSED;

        object? doc = null;
        object? folder = null;
        object? item = null;
        try
        {
            doc = shellWindow.GetType().InvokeMember("Document",
                BindingFlags.GetProperty, null, shellWindow, null);
            if (doc == null) return;

            folder = doc.GetType().InvokeMember("Folder",
                BindingFlags.GetProperty, null, doc, null);
            if (folder == null) return;

            item = folder.GetType().InvokeMember("ParseName",
                BindingFlags.InvokeMethod, null, folder, new object[] { fileName });
            if (item == null) return;

            doc.GetType().InvokeMember("SelectItem",
                BindingFlags.InvokeMethod, null, doc, new object[] { item, flags });
        }
        catch { /* ignore */ }
        finally
        {
            if (item != null) try { Marshal.ReleaseComObject(item); } catch { }
            if (folder != null) try { Marshal.ReleaseComObject(folder); } catch { }
            if (doc != null) try { Marshal.ReleaseComObject(doc); } catch { }
        }
    }

    private static IntPtr TryGetComHwnd(object win)
    {
        foreach (var name in new[] { "HWND", "Hwnd" })
        {
            try
            {
                var v = win.GetType().InvokeMember(name,
                    BindingFlags.GetProperty | BindingFlags.IgnoreCase, null, win, null);
                if (v == null) continue;
                if (v is IntPtr p) return p;
                return new IntPtr(Convert.ToInt64(v, System.Globalization.CultureInfo.InvariantCulture));
            }
            catch { /* ignore */ }
        }
        return IntPtr.Zero;
    }

    private static bool ExplorerFrameMatches(IntPtr explorerFrame, IntPtr comHwnd)
    {
        if (explorerFrame == IntPtr.Zero || comHwnd == IntPtr.Zero) return false;
        if (explorerFrame == comHwnd) return true;
        if (Win32.IsChild(explorerFrame, comHwnd)) return true;
        if (Win32.GetAncestor(comHwnd, Win32.GA_ROOT) == explorerFrame) return true;
        for (var w = comHwnd; w != IntPtr.Zero; w = Win32.GetParent(w))
            if (w == explorerFrame) return true;
        return false;
    }

    private static string? ReadShellWindowPath(object win)
    {
        object? doc = null, folder = null, self = null;
        try
        {
            doc = win.GetType().InvokeMember("Document", BindingFlags.GetProperty, null, win, null);
            if (doc == null) return null;
            folder = doc.GetType().InvokeMember("Folder", BindingFlags.GetProperty, null, doc, null);
            if (folder == null) return null;
            self = folder.GetType().InvokeMember("Self", BindingFlags.GetProperty, null, folder, null);
            if (self == null) return null;
            return self.GetType().InvokeMember("Path", BindingFlags.GetProperty, null, self, null)?.ToString();
        }
        catch { return null; }
        finally
        {
            if (self != null) Marshal.ReleaseComObject(self);
            if (folder != null) Marshal.ReleaseComObject(folder);
            if (doc != null) Marshal.ReleaseComObject(doc);
        }
    }

    private static string NormPath(string? p) => NormalizeFolderForEverything(p ?? "");

    /// <summary>SHOpenFolderAndSelectItems fallback：可复用已开窗口。</summary>
    private static void RevealViaShellApi(string fullPath)
    {
        var pidl = Win32.ILCreateFromPathW(fullPath);
        if (pidl == IntPtr.Zero) return;
        try
        {
            Win32.SHOpenFolderAndSelectItems(pidl, 0, null, 0);
        }
        finally
        {
            Win32.ILFree(pidl);
        }
    }

    // ===================== 辅助：快速上下文检测 =====================

}

