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
    public void RebuildPinyinSearchIndex(string mode)
    {
#if CLIPX_CLIPBOARD
        ClipboardEntry.PinyinFilterMode = PinyinFilterModes.Normalize(mode);
        ClipboardHistoryStore.PinyinFilterMode = ClipboardEntry.PinyinFilterMode;
        _historyStore.RebuildPinyinBlobs(ClipboardEntry.PinyinFilterMode);
        RefreshFilter();
#endif
    }

    private void EnsurePinyinSearchIndexVersion(AppSettings settings)
    {
#if CLIPX_CLIPBOARD
        if (PinyinFilterModes.Normalize(settings.PinyinFilterMode) != PinyinFilterModes.Xiaohe) return;
        if (settings.PinyinFilterIndexVersion >= PinyinFilterModes.CurrentIndexVersion) return;
        _historyStore.RebuildPinyinBlobs(settings.PinyinFilterMode);
        settings.PinyinFilterIndexVersion = PinyinFilterModes.CurrentIndexVersion;
        settings.Save();
#endif
    }
}
