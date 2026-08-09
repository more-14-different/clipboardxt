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
    private void UpdateBatchOrderProperties()
    {
        _batchQueue.ApplyOrderProperties(_allItems);
    }

    /// <summary>队列整体移到 <see cref="_allItems"/> 最前（与列表顶栏展示顺序一致）。</summary>
    private void ReorderAllItemsQueueFirst()
    {
        _batchQueue.ReorderAllItemsQueueFirst(_allItems);
    }
}
