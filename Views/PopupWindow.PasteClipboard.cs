using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
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
    private static void CleanupOldClipboardExports()
    {
        try
        {
            var dir = Path.Combine(Path.GetTempPath(), "ClipboardX");
            if (!Directory.Exists(dir)) return;
            var threshold = DateTime.UtcNow.AddHours(-24);
            foreach (var f in Directory.GetFiles(dir, "clip_*.*"))
            {
                try
                {
                    if (File.GetLastWriteTimeUtc(f) < threshold)
                        File.Delete(f);
                }
                catch { /* ignore */ }
            }
        }
        catch { /* ignore */ }
    }

    private AltVTextPasteSession CreateAltVTextPasteSession() =>
        new(
            _targetWindow,
            EnableExternalClipboardProviderForAltV,
            _appSettings?.PasteSimulationMode ?? PasteSimulationModes.CtrlV,
            TrySetTextLocallyAsync,
            text => TryDirectInsertTextToFocusedEditControl(_targetWindow, text),
            TryDirectTypeTextToTarget,
            SendCtrlVPaste,
            SendShiftInsertPaste);

    internal async Task FileJumpStandalonePasteAsync(string text, IntPtr targetWindow)
    {
        var previousPasteInProgress = _pasteInProgress;
        var previousIsSettingClipboard = _isSettingClipboard;
        _pasteInProgress = true;
        _isSettingClipboard = true;

        var session = new AltVTextPasteSession(
            targetWindow,
            EnableExternalClipboardProviderForAltV,
            _appSettings?.PasteSimulationMode ?? PasteSimulationModes.CtrlV,
            TrySetTextLocallyAsync,
            txt => TryDirectInsertTextToFocusedEditControl(targetWindow, txt),
            TryDirectTypeTextToTarget,
            SendCtrlVPaste,
            SendShiftInsertPaste);

        try
        {
            await session.ExecuteStandaloneClipboardPasteAsync(
                text,
                "filejump_paste",
                maxRetries: 5,
                delayMs: 20,
                markSelfWroteClipboard: MarkSelfWroteClipboard);
        }
        catch (Exception ex)
        {
            ClipboardDiagnosticsLog.Write(
                $"filejump_paste unexpected {ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            _isSettingClipboard = previousIsSettingClipboard;
            _pasteInProgress = previousPasteInProgress;
        }
    }

    private async Task<AltVTextPasteSession.ClipboardWriteResult> TrySetTextLocallyAsync(
        string text,
        int maxRetries,
        int delayMs)
    {
        var directResult = await ClipboardWriteRetry.TrySetDetailedAsync(
            () => System.Windows.Clipboard.SetText(text),
            $"SetText len={text.Length}",
            maxRetries: maxRetries,
            delayMs: delayMs,
            clipNudgeHwnd: _hwnd);
        return new AltVTextPasteSession.ClipboardWriteResult(directResult.Success, directResult.ClipboardLocked);
    }
}

