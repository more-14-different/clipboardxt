using System.Windows.Input;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;

namespace ClipboardManager;

public partial class PopupWindow
{
    private SearchEditorMouseController? _searchMouseController;

    private SearchEditorMouseController SearchMouseController =>
        _searchMouseController ??= new(
            SearchTextBlock,
            _searchEditor,
            UpdateSearchUI,
            IsPhysicalShiftDown);

    private void SearchTextBlock_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e) =>
        SearchMouseController.PreviewMouseLeftButtonDown(e);

    private void SearchTextBlock_PreviewMouseMove(object sender, MouseEventArgs e) =>
        SearchMouseController.PreviewMouseMove(e);

    private void SearchTextBlock_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e) =>
        SearchMouseController.PreviewMouseLeftButtonUp(e);

    private void SearchTextBlock_LostMouseCapture(object sender, MouseEventArgs e) =>
        _searchMouseController?.LostMouseCapture();

    private void EndSearchMouseSelection() =>
        _searchMouseController?.EndSelection();
}
