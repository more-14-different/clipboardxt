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
    private async Task<(
        bool ClipboardOk,
        bool UsedNonClipboardTextInsert,
        AltVClipboardProvider.Session? ProviderSession)> PrepareTextEntryClipboardAsync(
            ClipboardEntry item,
            AltVTextPasteSession textPasteSession,
            bool sendNewlineAfterTextWhenAltEnterBatch,
            bool noSegmentDelays,
            int clipRetries,
            int clipRetryDelayMs)
    {
        var clipText = sendNewlineAfterTextWhenAltEnterBatch && noSegmentDelays
            ? item.TextContent! + Environment.NewLine
            : item.TextContent!;
        var textClipboard = await textPasteSession.PrepareClipboardAsync(
            clipText,
            "paste text",
            maxRetries: noSegmentDelays ? Math.Min(clipRetries, 3) : 1,
            delayMs: clipRetryDelayMs);

        var providerSession = textClipboard.ProviderSession;
        var clipboardResult = textClipboard.Result;
        var clipboardOk = clipboardResult.Success;
        var usedNonClipboardTextInsert = false;
        if (clipboardOk)
        {
            MarkSelfWroteClipboard();
        }
        else
        {
            clipboardOk = textPasteSession.TryInsertWithoutClipboard(
                clipText,
                "paste text",
                out usedNonClipboardTextInsert);
        }

        return (clipboardOk, usedNonClipboardTextInsert, providerSession);
    }
}
