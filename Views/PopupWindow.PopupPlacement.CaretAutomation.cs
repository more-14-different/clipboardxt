using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;

namespace ClipboardManager;

public partial class PopupWindow : Window
{
    private readonly record struct CaretAutomationResult(
        bool Ok,
        double X,
        double Y,
        string Source);

    private sealed class CaretAutomationProbe
    {
        public CaretAutomationProbe(Task<CaretAutomationResult> task, long startedTickMs)
        {
            Task = task;
            StartedTickMs = startedTickMs;
        }

        public Task<CaretAutomationResult> Task { get; }
        public long StartedTickMs { get; }
    }

    private static CaretAutomationProbe StartCaretAutomationProbe()
    {
        var startedTickMs = Environment.TickCount64;
        var task = Task.Run(() =>
        {
            try
            {
                var focused = System.Windows.Automation.AutomationElement.FocusedElement;
                if (focused == null)
                    return new CaretAutomationResult(false, 0, 0, "no-focused");

                var className = focused.Current.ClassName;

                if (focused.TryGetCurrentPattern(
                        System.Windows.Automation.TextPattern.Pattern, out var p))
                {
                    var sel = ((System.Windows.Automation.TextPattern)p).GetSelection();
                    if (sel.Length > 0)
                    {
                        var rects = sel[0].GetBoundingRectangles();
                        if (rects.Length > 0 && (rects[0].X > 0 || rects[0].Y > 0))
                        {
                            // Chromium can expose its whole content surface as a fake
                            // selection rectangle with an empty automation class.
                            if (!PopupPlacementCalculator.HasUsableAutomationClassName(className))
                            {
                                return new CaretAutomationResult(
                                    false,
                                    0,
                                    0,
                                    "text-sel-empty-cls");
                            }

                            return new CaretAutomationResult(
                                true,
                                rects[0].X,
                                rects[0].Bottom + 4,
                                "text-sel");
                        }
                    }
                }

                var rect = focused.Current.BoundingRectangle;
                if (!rect.IsEmpty && rect.Width > 0 && rect.Height > 0)
                {
                    if (!PopupPlacementCalculator.HasUsableAutomationClassName(className))
                    {
                        return new CaretAutomationResult(
                            false,
                            0,
                            0,
                            "bound-rect-empty-cls");
                    }

                    var foregroundWindow = Win32.GetForegroundWindow();
                    if (foregroundWindow != IntPtr.Zero
                        && Win32.GetWindowRect(foregroundWindow, out var foregroundRect)
                        && PopupPlacementCalculator.CoversForegroundWindow(
                            rect.Width,
                            rect.Height,
                            foregroundRect.Right - foregroundRect.Left,
                            foregroundRect.Bottom - foregroundRect.Top))
                    {
                        return new CaretAutomationResult(
                            false,
                            0,
                            0,
                            "bound-rect-window-level");
                    }

                    return new CaretAutomationResult(
                        true,
                        rect.X + 20,
                        rect.Bottom + 4,
                        "bound-rect");
                }

                return new CaretAutomationResult(false, 0, 0, "no-pattern-no-rect");
            }
            catch (Exception ex)
            {
                return new CaretAutomationResult(false, 0, 0, "ex:" + ex.GetType().Name);
            }
        });

        return new CaretAutomationProbe(task, startedTickMs);
    }

    private static bool TryGetCaretByAutomation(
        CaretAutomationProbe? probe,
        out double x,
        out double y)
    {
        x = y = 0;
        var sw = Stopwatch.StartNew();
        string outcome = "init";
        string source = "none";
        probe ??= StartCaretAutomationProbe();
        try
        {
            // Word/Office/Chromium 走 UIA 文本模式，冷启动 200ms 常超时；放宽到 500ms 才能首次命中
            // 首次 Show 时探测会在布局前启动；这里只等待尚未消耗的预算，让 UIA 与 WPF 冷布局并行。
            var elapsedBeforeWait = Math.Max(
                0,
                Environment.TickCount64 - probe.StartedTickMs);
            var remainingBudgetMs = (int)Math.Max(0, 500 - elapsedBeforeWait);
            if (probe.Task.IsCompleted || probe.Task.Wait(remainingBudgetMs))
            {
                var result = probe.Task.Result;
                source = result.Source;
                if (result.Ok)
                {
                    x = result.X;
                    y = result.Y;
                    outcome = "ok";
                    return true;
                }
                outcome = "miss";
            }
            else
            {
                outcome = "timeout-budget";
            }
        }
        catch (Exception ex) { outcome = "ex:" + ex.GetType().Name; }
        finally
        {
            sw.Stop();
            #region agent log
            AgentDbgLog("H23", "TryGetCaretByAutomation", outcome,
                new
                {
                    waitMs = sw.ElapsedMilliseconds,
                    totalMs = Environment.TickCount64 - probe.StartedTickMs,
                    source,
                    x,
                    y
                });
            #endregion
        }
        return false;
    }

    /// <summary>启动后异步预热 UIA TextPattern 代理，避免首次从 Word/Office 调出剪贴板时 UIA 200~500ms 冷启动导致定位落到鼠标兜底。</summary>
    private static void WarmUpUiaCaretProxy()
    {
        Task.Run(() =>
        {
            try
            {
                var focused = System.Windows.Automation.AutomationElement.FocusedElement;
                if (focused != null)
                {
                    _ = focused.TryGetCurrentPattern(
                        System.Windows.Automation.TextPattern.Pattern, out _);
                    _ = focused.Current.BoundingRectangle;
                }
            }
            catch { }
        });
    }
}
