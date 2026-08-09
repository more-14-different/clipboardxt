using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace ClipboardManager;

public partial class SettingsWindow : Window
{    private void OnCustomFileDialogRulesChanged()
    {
        if (Dispatcher.CheckAccess())
            ReloadCustomFileDialogList();
        else
            Dispatcher.Invoke(ReloadCustomFileDialogList);
    }

    private void ReloadCustomFileDialogList()
    {
        CustomRulesList.Items.Clear();
        foreach (var r in CustomFileDialogStore.GetRules())
            CustomRulesList.Items.Add(r);
    }

    private void CustomRuleDelete_Click(object sender, RoutedEventArgs e)
    {
        if (CustomRulesList.SelectedItem is not CustomFileDialogRule r)
            return;

        if (LocalizedMessageBox.Show(
                $"删除此规则？\n{r.SummaryLine}",
                "确认",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Question) != MessageBoxResult.OK)
            return;

        CustomFileDialogStore.RemoveRule(r.Id);
        ReloadCustomFileDialogList();
    }

    private void CustomRuleWizard_Click(object sender, RoutedEventArgs e)
    {
        if (System.Windows.Application.Current is App app)
            app.StartCustomFileDialogWizard();
    }

    private void CustomRuleImportMerge_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "JSON (*.json)|*.json|所有文件 (*.*)|*.*",
            Title = "导入规则（与本地合并）",
        };
        if (dlg.ShowDialog(this) != true)
            return;

        var n = CustomFileDialogStore.ImportMergeFromFile(dlg.FileName, out var err);
        if (err != null)
        {
            LocalizedMessageBox.Show("导入失败：\n" + err, "自定义文件对话框",
                MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        LocalizedMessageBox.Show(
            n > 0 ? $"已合并写入 {n} 条有效规则。" : "文件中没有可导入的有效规则（需包含非空的 windowClass）。",
            "自定义文件对话框",
            MessageBoxButton.OK,
            n > 0 ? MessageBoxImage.Information : MessageBoxImage.Warning);
    }

    private void CustomRuleImportReplace_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "JSON (*.json)|*.json|所有文件 (*.*)|*.*",
            Title = "导入规则（完全替换）",
        };
        if (dlg.ShowDialog(this) != true)
            return;

        if (LocalizedMessageBox.Show(
                "将删除当前所有自定义规则，并替换为文件中的列表。\n确定继续？",
                "确认替换",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Warning) != MessageBoxResult.OK)
            return;

        var n = CustomFileDialogStore.ImportReplaceFromFile(dlg.FileName, out var err);
        if (err != null)
        {
            LocalizedMessageBox.Show("导入失败：\n" + err, "自定义文件对话框",
                MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        LocalizedMessageBox.Show(
            $"已替换为 {n} 条规则。",
            "自定义文件对话框",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void CustomRuleExport_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "JSON (*.json)|*.json",
            FileName = "clipboardx_custom_file_dialogs.json",
            Title = "导出自定义文件对话框规则",
        };
        if (dlg.ShowDialog(this) != true)
            return;

        try
        {
            CustomFileDialogStore.ExportToFile(dlg.FileName);
            LocalizedMessageBox.Show("导出完成。", "自定义文件对话框",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            LocalizedMessageBox.Show("导出失败：\n" + ex.Message, "自定义文件对话框",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}

