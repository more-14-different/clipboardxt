using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;

namespace ClipboardManager;

internal sealed class SearchEditorMouseController
{
    private readonly TextBlock _textBlock;
    private readonly SearchEditorState _state;
    private readonly Action _refresh;
    private readonly Func<bool> _isShiftDown;
    private readonly List<Run> _characterRuns = new();
    private Rect[] _characterBounds = [];
    private int _dragAnchor;
    private bool _isSelecting;

    public SearchEditorMouseController(
        TextBlock textBlock,
        SearchEditorState state,
        Action refresh,
        Func<bool> isShiftDown)
    {
        _textBlock = textBlock;
        _state = state;
        _refresh = refresh;
        _isShiftDown = isShiftDown;
    }

    public void Render(Brush normalForeground, Brush accent)
    {
        _characterRuns.Clear();
        _textBlock.Inlines.Clear();
        _state.TryGetSelection(out var selectionStart, out var selectionEnd);

        for (var i = 0; i <= _state.Text.Length; i++)
        {
            if (i == _state.CaretIndex)
            {
                _textBlock.Inlines.Add(new Run("|")
                {
                    Foreground = accent,
                    FontSize = 13,
                    FontWeight = FontWeights.SemiBold
                });
            }

            if (i >= _state.Text.Length) continue;

            var selected = i >= selectionStart && i < selectionEnd;
            var characterRun = new Run(SearchEditorText.ToDisplayCharacter(_state.Text[i]))
            {
                Foreground = selected ? Brushes.White : normalForeground,
                Background = selected ? accent : null,
                FontSize = 13,
                FontWeight = FontWeights.Normal
            };
            _characterRuns.Add(characterRun);
            _textBlock.Inlines.Add(characterRun);
        }
    }

    public void ClearVisual()
    {
        _characterRuns.Clear();
        _characterBounds = [];
        _textBlock.Inlines.Clear();
    }

    public void PreviewMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        if (!_state.HasText) return;

        CaptureLayout();
        var point = e.GetPosition(_textBlock);
        var caretIndex = SearchEditorMouseHitTest.FindCaretIndex(_characterBounds, point);

        if (e.ClickCount >= 2)
        {
            var characterIndex = SearchEditorMouseHitTest.FindCharacterIndex(_characterBounds, point);
            if (SearchEditorSelection.TryGetUnitRange(_state.Text, characterIndex, out var range))
                _state.SetSelection(range.Start, range.End);
            else
                _state.MoveCaret(caretIndex, extendSelection: false);

            _refresh();
            EndSelection();
            e.Handled = true;
            return;
        }

        _dragAnchor = _isShiftDown()
            ? (_state.SelectionAnchor >= 0 ? _state.SelectionAnchor : _state.CaretIndex)
            : caretIndex;
        ApplySelection(caretIndex);
        _isSelecting = _textBlock.CaptureMouse();
        e.Handled = true;
    }

    public void PreviewMouseMove(MouseEventArgs e)
    {
        if (!_isSelecting) return;
        if (e.LeftButton != MouseButtonState.Pressed)
        {
            EndSelection();
            return;
        }

        var caretIndex = SearchEditorMouseHitTest.FindCaretIndex(
            _characterBounds,
            e.GetPosition(_textBlock));
        ApplySelection(caretIndex);
        e.Handled = true;
    }

    public void PreviewMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        if (!_isSelecting) return;
        var caretIndex = SearchEditorMouseHitTest.FindCaretIndex(
            _characterBounds,
            e.GetPosition(_textBlock));
        ApplySelection(caretIndex);
        EndSelection();
        e.Handled = true;
    }

    public void LostMouseCapture() =>
        _isSelecting = false;

    public void EndSelection()
    {
        _isSelecting = false;
        if (_textBlock.IsMouseCaptured)
            _textBlock.ReleaseMouseCapture();
    }

    private void ApplySelection(int caretIndex)
    {
        caretIndex = Math.Clamp(caretIndex, 0, _state.Text.Length);
        var selectionAnchor = caretIndex == _dragAnchor ? -1 : _dragAnchor;
        _state.SetSelection(selectionAnchor, caretIndex);
        _refresh();
    }

    private void CaptureLayout()
    {
        _textBlock.UpdateLayout();
        _characterBounds = new Rect[_characterRuns.Count];

        for (var i = 0; i < _characterRuns.Count; i++)
        {
            var bounds = _characterRuns[i].ContentStart.GetCharacterRect(LogicalDirection.Forward);
            _characterBounds[i] = bounds;
        }
    }
}
