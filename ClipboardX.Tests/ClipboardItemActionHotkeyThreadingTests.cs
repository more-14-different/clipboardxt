using System.Reflection;
using System.Runtime.CompilerServices;
using ClipboardManager;

namespace ClipboardX.Tests;

public sealed class ClipboardItemActionHotkeyThreadingTests
{
    [Fact]
    public void EscapeMatching_DoesNotReadWpfControlsBeforeShortcutResolution()
    {
        // 不运行 Window 构造函数，让所有 WPF 控件字段保持 null；Esc 匹配阶段若再次读取
        // ContextPopup/ItemsList，本测试会立即以 NullReferenceException 失败。
        var window = (PopupWindow)RuntimeHelpers.GetUninitializedObject(typeof(PopupWindow));
        typeof(PopupWindow)
            .GetField("_appSettings", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(window, new AppSettings());

        var handled = (bool)typeof(PopupWindow)
            .GetMethod("TryDispatchClipboardItemActionHotkey", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(window, [Win32.VK_ESCAPE])!;

        Assert.False(handled);
    }
}
