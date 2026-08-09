using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Brush = System.Windows.Media.Brush;
using Button = System.Windows.Controls.Button;
using Orientation = System.Windows.Controls.Orientation;
using TextBox = System.Windows.Controls.TextBox;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using ClipboardManager.Models;

namespace ClipboardManager;

public partial class PopupWindow : Window
{
    private const int ClipbrdECantOpenHResult = unchecked((int)0x800401D0);

    private static bool IsClipboardCantOpen(Exception ex) =>
        ex is COMException com && com.HResult == ClipbrdECantOpenHResult;

    /// <summary>
    /// 读剪贴板时其它进程常短时占用 → CLIPBRD_E_CANT_OPEN；与 TrySetClipboard 类似做短暂重试，避免 monitor outer catch 误判整次更新失败。
    /// </summary>
    private static bool TryReadClipboardBool(Func<bool> read, string tag, int maxRetries = 4, int delayMs = 5)
    {
        for (var i = 0; i < maxRetries; i++)
        {
            try { return read(); }
            catch (Exception ex)
            {
                if (IsClipboardCantOpen(ex))
                {
                    if (i == maxRetries - 1)
                        ClipboardDiagnosticsLog.Write(
                            $"monitor read {tag} gave_up retries={maxRetries} CLIPBRD_E_CANT_OPEN");
                    else
                        Thread.Sleep(delayMs);
                    continue;
                }
                ClipboardDiagnosticsLog.Write($"monitor read {tag} {ex.GetType().Name}: {ex.Message}");
                return false;
            }
        }
        return false;
    }

    private static T? TryReadClipboard<T>(Func<T> read, string tag, int maxRetries = 4, int delayMs = 5) where T : class
    {
        for (var i = 0; i < maxRetries; i++)
        {
            try { return read(); }
            catch (Exception ex)
            {
                if (IsClipboardCantOpen(ex))
                {
                    if (i == maxRetries - 1)
                        ClipboardDiagnosticsLog.Write(
                            $"monitor read {tag} gave_up retries={maxRetries} CLIPBRD_E_CANT_OPEN");
                    else
                        Thread.Sleep(delayMs);
                    continue;
                }
                ClipboardDiagnosticsLog.Write($"monitor read {tag} {ex.GetType().Name}: {ex.Message}");
                return default;
            }
        }
        return default;
    }
}
