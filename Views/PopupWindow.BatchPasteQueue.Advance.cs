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
#if CLIPX_CLIPBOARD
    private void MarkAwaitingBatchQueueNextPasteToSwitchToNormalIfEnabled()
    {
        var mode = GetBatchMode();
        if (mode != BatchPasteQueueMode.Fifo && mode != BatchPasteQueueMode.Lifo) return;
        if (!(_appSettings?.BatchQueueAutoSwitchToNormalAfterQueueDone ?? true)) return;
        _batchQueueAwaitingNextPasteToSwitchOff = true;
    }

    private void TryAdvancePasteQueueAfterGlobalPaste()
    {
        if (GetBatchMode() == BatchPasteQueueMode.Off) return;
        if (!_batchQueue.TryAdvance(out var done) || done == null) return;
        if (!done.IsQuickPaste)
        {
            done.TouchCopiedTime();
            if (done.PersistedId is long pid)
                _historyStore.TryUpdateCopiedAt(pid, done.CopiedAt);
        }
        UpdateBatchOrderProperties();
        ReorderAllItemsQueueFirst();
        RefreshFilter(0);
        if (_batchQueue.Count == 0 && (GetBatchMode() == BatchPasteQueueMode.Fifo || GetBatchMode() == BatchPasteQueueMode.Lifo))
            MarkAwaitingBatchQueueNextPasteToSwitchToNormalIfEnabled();
        SyncBatchPasteKeyboardHook();
        _ = TryPushClipboardQueueHeadAsync();
    }
#endif
}
