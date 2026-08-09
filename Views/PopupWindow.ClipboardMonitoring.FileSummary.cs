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
    /// <summary>日志用：只记录路径数量和首路径；不得为日志逐项访问文件系统。</summary>
    private static string SummarizeFileDropForLog(string[] paths)
    {
        if (paths.Length == 0) return "(empty)";
        var first = paths[0];
        if (first.Length > 120) first = first[..117] + "...";
        return $"count={paths.Length} first=\"{first}\"";
    }
}
