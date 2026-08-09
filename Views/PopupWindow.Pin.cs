using System.Windows.Input;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;

namespace ClipboardManager;

public partial class PopupWindow
{
    private bool HidePopupAfterUserAction => !_popupPinned;

    private void Pin_Click(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        _popupPinned = !_popupPinned;
        UpdatePinHeaderUi();
    }

    private void UpdatePinHeaderUi()
    {
        if (PinHeaderBorder == null || PinHeaderIcon == null) return;
        PinHeaderBorder.Background = _popupPinned
            ? (Brush)FindResource("BatchModeTagFill")
            : Brushes.Transparent;
        PinHeaderIcon.Foreground = _popupPinned
            ? (Brush)FindResource("BatchModeTagFg")
            : (Brush)FindResource("SecondaryText");
        PinHeaderBorder.ToolTip = _popupPinned
            ? "已置顶：粘贴后保持窗口打开；再次点击取消或按 Esc 关闭"
            : "置顶：粘贴后保持窗口打开，便于连续粘贴";
    }
}
