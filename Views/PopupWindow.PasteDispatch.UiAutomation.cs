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
    /// <summary>
    /// 第二层非剪贴板文本插入：对支持 UIA ValuePattern 的控件，按当前选区重建完整文本再 SetValue。
    /// 比 Unicode 注入更接近“粘贴语义”，但仍避免写系统剪贴板。
    /// </summary>
    private static bool TryReplaceFocusedTextViaUiAutomation(IntPtr targetWindow, string text)
    {
        if (targetWindow == IntPtr.Zero || string.IsNullOrEmpty(text) || !Win32.IsWindow(targetWindow))
            return false;

        try
        {
            var focused = System.Windows.Automation.AutomationElement.FocusedElement;
            if (focused == null)
                return false;

            var nativeHandle = new IntPtr(focused.Current.NativeWindowHandle);
            if (nativeHandle != IntPtr.Zero
                && nativeHandle != targetWindow
                && Win32.GetAncestor(nativeHandle, Win32.GA_ROOT) != targetWindow)
            {
                return false;
            }

            if (!focused.TryGetCurrentPattern(
                    System.Windows.Automation.ValuePattern.Pattern, out var valuePatternObj))
            {
                return false;
            }

            var valuePattern = (System.Windows.Automation.ValuePattern)valuePatternObj;
            if (valuePattern.Current.IsReadOnly)
                return false;

            var currentValue = valuePattern.Current.Value ?? string.Empty;
            if (TryGetFocusedSelectionOffsetsViaUiAutomation(focused, currentValue, out var start, out var end))
            {
                start = MapNormalizedOffsetToOriginalIndex(currentValue, start);
                end = MapNormalizedOffsetToOriginalIndex(currentValue, end);
                valuePattern.SetValue(currentValue[..start] + text + currentValue[end..]);
                return true;
            }

            if (currentValue.Length == 0)
            {
                valuePattern.SetValue(text);
                return true;
            }
        }
        catch (Exception ex)
        {
            ClipboardDiagnosticsLog.Write($"paste text uiaReplace EX {ex.GetType().Name}: {ex.Message}");
        }

        return false;
    }

    private static bool TryGetFocusedSelectionOffsetsViaUiAutomation(
        System.Windows.Automation.AutomationElement focused,
        string currentValue,
        out int start,
        out int end)
    {
        start = 0;
        end = 0;

        if (!focused.TryGetCurrentPattern(
                System.Windows.Automation.TextPattern.Pattern, out var textPatternObj))
        {
            return false;
        }

        var textPattern = (System.Windows.Automation.TextPattern)textPatternObj;
        var selections = textPattern.GetSelection();
        if (selections == null || selections.Length == 0)
            return false;

        try
        {
            var selection = selections[0];
            var document = textPattern.DocumentRange;
            if (document == null)
                return false;

            var beforeSelection = document.Clone();
            beforeSelection.MoveEndpointByRange(
                System.Windows.Automation.Text.TextPatternRangeEndpoint.End,
                selection,
                System.Windows.Automation.Text.TextPatternRangeEndpoint.Start);

            var beforeText = beforeSelection.GetText(-1) ?? string.Empty;
            start = NormalizeUiAutomationTextForOffset(beforeText).Length;

            var throughSelection = document.Clone();
            throughSelection.MoveEndpointByRange(
                System.Windows.Automation.Text.TextPatternRangeEndpoint.End,
                selection,
                System.Windows.Automation.Text.TextPatternRangeEndpoint.End);

            var throughText = throughSelection.GetText(-1) ?? string.Empty;
            end = NormalizeUiAutomationTextForOffset(throughText).Length;

            if (start > currentValue.Length || end > currentValue.Length)
            {
                start = Math.Min(start, currentValue.Length);
                end = Math.Min(end, currentValue.Length);
            }

            return end >= start;
        }
        catch (Exception ex)
        {
            ClipboardDiagnosticsLog.Write($"paste text uiaSelection EX {ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }

    private static string NormalizeUiAutomationTextForOffset(string text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        // UIA often normalizes line endings to CRLF. Align offsets with the .NET string we later splice.
        return text.Replace("\r\n", "\n").Replace('\r', '\n');
    }

    private static int MapNormalizedOffsetToOriginalIndex(string original, int normalizedOffset)
    {
        if (normalizedOffset <= 0 || string.IsNullOrEmpty(original))
            return 0;

        var seen = 0;
        for (var i = 0; i < original.Length; i++)
        {
            if (seen >= normalizedOffset)
                return i;

            if (original[i] == '\r')
            {
                if (i + 1 < original.Length && original[i + 1] == '\n')
                    i++;
                seen++;
                continue;
            }

            seen++;
        }

        return original.Length;
    }
}
