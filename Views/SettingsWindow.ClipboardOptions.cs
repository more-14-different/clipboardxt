using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace ClipboardManager;

public partial class SettingsWindow : Window
{    private static string ModifierDisplayName(string m) => m switch
    {
        "Alt" => "Alt",
        "Win" => "Win",
        "CapsLock" => "CapsLock",
        _ => "Ctrl",
    };

    private static string PasteSimulationDisplayName(string m) =>
        PasteSimulationModes.Normalize(m) == PasteSimulationModes.ShiftInsert ? "Shift+Insert" : "Ctrl+V";

    private void PasteSimulationCycle_Click(object sender, RoutedEventArgs e)
    {
        _pendingPasteSimulationMode = _pendingPasteSimulationMode == PasteSimulationModes.CtrlV
            ? PasteSimulationModes.ShiftInsert
            : PasteSimulationModes.CtrlV;
        PasteSimulationText.Text = PasteSimulationDisplayName(_pendingPasteSimulationMode);
    }

    private void PasteDoubleClickCycle_Click(object sender, MouseButtonEventArgs e)
    {
        _pendingPasteRequiresDoubleClick = !_pendingPasteRequiresDoubleClick;
        PasteDoubleClickText.Text = _pendingPasteRequiresDoubleClick ? "开启" : "关闭";
    }

    private void ModifierCycle_Click(object sender, RoutedEventArgs e)
    {
        int idx = Array.IndexOf(ModifierOptions, _pendingModifierKey);
        _pendingModifierKey = ModifierOptions[(idx + 1) % ModifierOptions.Length];
        ModifierText.Text = ModifierDisplayName(_pendingModifierKey);
    }

    private void BatchPasteMergeCycle_Click(object sender, RoutedEventArgs e)
    {
        _pendingBatchPasteMergeText = !_pendingBatchPasteMergeText;
        BatchPasteMergeToggleText.Text = _pendingBatchPasteMergeText ? "开启" : "关闭";
    }

    private void BatchQueueAutoNormalCycle_Click(object sender, RoutedEventArgs e)
    {
        _pendingBatchQueueAutoSwitchToNormalAfterQueueDone = !_pendingBatchQueueAutoSwitchToNormalAfterQueueDone;
        BatchQueueAutoNormalToggleText.Text = _pendingBatchQueueAutoSwitchToNormalAfterQueueDone ? "开启" : "关闭";
    }

    private void SearchColdArchivesCycle_Click(object sender, RoutedEventArgs e)
    {
        _pendingSearchColdArchives = !_pendingSearchColdArchives;
        SearchColdArchivesText.Text = _pendingSearchColdArchives ? "开启" : "关闭";
    }

    private void OpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (OpacityValueText == null) return;
        _pendingOpacity = Math.Round(e.NewValue, 2);
        OpacityValueText.Text = $"{(int)(_pendingOpacity * 100)}%";
    }

    private void ClearHistory_Click(object sender, RoutedEventArgs e)
    {
        if (LocalizedMessageBox.Show("确定要清空所有剪切板历史？\n（快捷短语不受影响）",
                "确认清空", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
        {
            ClearHistoryRequested?.Invoke();
        }
    }
}

