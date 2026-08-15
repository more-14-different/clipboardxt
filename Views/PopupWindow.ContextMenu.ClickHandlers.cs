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
    private void CtxPaste_Click(object sender, MouseButtonEventArgs e) => ActivateCtxPaste();

    private void CtxLinePaste_Click(object sender, MouseButtonEventArgs e) => ActivateCtxLinePaste();

    private void CtxSoftLinePaste_Click(object sender, MouseButtonEventArgs e) => ActivateCtxSoftLinePaste();

    private void CtxOpenUrls_Click(object sender, MouseButtonEventArgs e) => ActivateCtxOpenUrls();

    private void CtxPasteAsFile_Click(object sender, MouseButtonEventArgs e) => ActivateCtxPasteAsFile();

    private void CtxPasteJsonFile_Click(object sender, MouseButtonEventArgs e) => ActivateCtxPasteJsonFile();

    private void CtxShortcut_Click(object sender, MouseButtonEventArgs e) => ActivateCtxShortcut();

    private void CtxStar_Click(object sender, MouseButtonEventArgs e) => ActivateCtxStar();

    private void CtxDelete_Click(object sender, MouseButtonEventArgs e) => ActivateCtxDelete();
}
