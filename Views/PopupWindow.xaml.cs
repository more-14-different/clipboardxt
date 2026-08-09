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
    public PopupWindow()
    {
        InitializeComponent();
        ItemsList.ItemsSource = _displayItems;
        ItemsList.SelectionChanged += ItemsList_SelectionChanged;

        PhraseEditPopup.Opened += (s, e) => _isPhraseEditPopupOpen = true;
        PhraseEditPopup.Closed += (s, e) => _isPhraseEditPopupOpen = false;
        TextEntryEditPopup.Opened += (s, e) => _isTextEntryEditPopupOpen = true;
        TextEntryEditPopup.Closed += (s, e) => _isTextEntryEditPopupOpen = false;
        BatchMenuPopup.Opened += (s, e) => _isBatchMenuPopupOpen = true;
        BatchMenuPopup.Closed += (s, e) => _isBatchMenuPopupOpen = false;
        ContextPopup.Opened += (s, e) => _isContextPopupOpen = true;
        ContextPopup.Closed += (s, e) => _isContextPopupOpen = false;
    }

    protected override void OnPreviewMouseDown(MouseButtonEventArgs e)
    {
        _clickReceivedByPopup = true;
        base.OnPreviewMouseDown(e);
    }


}
