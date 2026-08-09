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
    /// <summary>
    /// 去重时从队列移除了条目，队首可能已变；须把剪贴板重新对齐队首（与 AutoBatchEnqueue 中「FIFO 队尾追加不推队首」配合）。
    /// </summary>
    private void RequestBatchQueueHeadClipboardResyncAfterDedup()
    {
        if (GetBatchMode() == BatchPasteQueueMode.Off || _batchQueue.Count == 0) return;
        _ = TryPushClipboardQueueHeadAsync();
    }

    /// <summary>
    /// FIFO 下新复制入队尾时队首引用不变，反复 Set 队首会与系统剪贴板互锁并卡顿；仅当队首变化或 LIFO 时再写剪贴板。
    /// </summary>
    private void SchedulePushBatchQueueHeadIfChanged(ClipboardEntry? headBeforeMutation, BatchPasteQueueMode mode)
    {
        var headAfter = _batchQueue.Head;
        if (mode == BatchPasteQueueMode.Fifo && ReferenceEquals(headBeforeMutation, headAfter))
            return;
        _ = TryPushClipboardQueueHeadAsync();
    }
#endif
}
