using System.Windows.Input;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;

namespace ClipboardManager;

public partial class PopupWindow
{
    private SearchEditorMouseController? _phraseMouseController;

    private SearchEditorMouseController PhraseMouseController =>
        _phraseMouseController ??= new(
            PhraseEditDisplay,
            _phraseEditor,
            RefreshPhraseEditDisplay,
            IsPhysicalShiftDown);

    private void PhraseEditDisplay_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e) =>
        PhraseMouseController.PreviewMouseLeftButtonDown(e);

    private void PhraseEditDisplay_PreviewMouseMove(object sender, MouseEventArgs e) =>
        PhraseMouseController.PreviewMouseMove(e);

    private void PhraseEditDisplay_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e) =>
        PhraseMouseController.PreviewMouseLeftButtonUp(e);

    private void PhraseEditDisplay_LostMouseCapture(object sender, MouseEventArgs e) =>
        _phraseMouseController?.LostMouseCapture();

    private void EndPhraseMouseSelection() =>
        _phraseMouseController?.EndSelection();
}
