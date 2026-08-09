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
    public void ClearHistory()
    {
#if CLIPX_CLIPBOARD
        _historyStore.DeleteAll();
        _allItems.RemoveAll(x => !x.IsQuickPaste && string.IsNullOrWhiteSpace(x.ShortcutPhrase));
        _batchQueue.Clear();
        if (_batchQueueProviderSession != null)
        {
            _ = _batchQueueProviderSession.DisposeAsync();
            _batchQueueProviderSession = null;
        }
        UpdateBatchOrderProperties();
        RefreshFilter();
        SyncBatchPasteKeyboardHook();
#endif
    }
}
