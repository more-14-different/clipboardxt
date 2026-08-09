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
    /// <summary>非文本路径仍复用相同的目标窗口 heuristics。</summary>
    private void SendPasteToTarget()
    {
        var decision = PasteTargetHeuristics.DecideMode(_targetWindow, _appSettings?.PasteSimulationMode ?? PasteSimulationModes.CtrlV);
        if (decision.Mode == PasteSimulationModes.ShiftInsert)
            SendShiftInsertPaste();
        else
            SendCtrlVPaste();
    }

    // Shift+Insert 是系统级粘贴，但 Excel 等软件对模拟输入更挑：
    // 1) Insert 必须按扩展键发送；
    // 2) 若呼出面板时 Ctrl/Alt/Win 仍处于按下态，最终组合键会被污染。
    // 因此这里先等待用户物理松开所有修饰键，然后强制清除遗留的修饰键状态，再发送标准的 Shift+Insert。
    internal static void SendShiftInsertPaste()
    {
        // 增加等待物理按键释放，避免系统级按键状态冲突导致模拟按键失效（例如出现只输入 v 的情况）
        Win32.WaitForModifiersReleased(500);

        if (ReleaseHeldModifiers())
        {
            Thread.Sleep(1);
        }

        var combo = new Win32.INPUT[4];
        combo[0].type = Win32.INPUT_KEYBOARD; combo[0].u.ki.wVk = Win32.VK_SHIFT;
        combo[1].type = Win32.INPUT_KEYBOARD; combo[1].u.ki.wVk = Win32.VK_INSERT; combo[1].u.ki.dwFlags = Win32.KEYEVENTF_EXTENDEDKEY;
        combo[2].type = Win32.INPUT_KEYBOARD; combo[2].u.ki.wVk = Win32.VK_INSERT; combo[2].u.ki.dwFlags = Win32.KEYEVENTF_EXTENDEDKEY | Win32.KEYEVENTF_KEYUP;
        combo[3].type = Win32.INPUT_KEYBOARD; combo[3].u.ki.wVk = Win32.VK_SHIFT; combo[3].u.ki.dwFlags = Win32.KEYEVENTF_KEYUP;
        Win32.SendInput((uint)combo.Length, combo, Marshal.SizeOf<Win32.INPUT>());
    }

    /// <summary>先等待用户物理松开所有修饰键，然后强制释放可能遗留的修饰键状态，再发送 Ctrl+V。
    /// 因为如果物理按键仍被按下，仅靠模拟发送 KEYUP 往往会被系统状态覆盖，导致目标窗口按键识别错乱（例如漏掉 Ctrl 只输入 v 字符）。
    /// 释放遗留状态与发送真实组合键拆分两次 SendInput 提交，中间强制切换线程上下文，确保目标窗口消息队列有序响应。</summary>
    internal static void SendCtrlVPaste()
    {
        // 增加等待物理按键释放，避免系统级按键状态冲突导致模拟按键失效（例如出现只输入 v 的情况）
        Win32.WaitForModifiersReleased(500);

        if (ReleaseHeldModifiers())
        {
            // 让目标线程处理一帧释放事件，再注入组合键。极短让步（Sleep 0 即可触发线程切换）。
            Thread.Sleep(1);
        }

        var combo = new Win32.INPUT[4];
        combo[0].type = Win32.INPUT_KEYBOARD; combo[0].u.ki.wVk = Win32.VK_CONTROL;
        combo[1].type = Win32.INPUT_KEYBOARD; combo[1].u.ki.wVk = Win32.VK_V;
        combo[2].type = Win32.INPUT_KEYBOARD; combo[2].u.ki.wVk = Win32.VK_V; combo[2].u.ki.dwFlags = Win32.KEYEVENTF_KEYUP;
        combo[3].type = Win32.INPUT_KEYBOARD; combo[3].u.ki.wVk = Win32.VK_CONTROL; combo[3].u.ki.dwFlags = Win32.KEYEVENTF_KEYUP;
        Win32.SendInput((uint)combo.Length, combo, Marshal.SizeOf<Win32.INPUT>());
    }

    private void SendSoftLineBreakToTarget()
    {
        if (_targetWindow != IntPtr.Zero && Win32.IsWindow(_targetWindow))
            Win32.SetForegroundWindowAggressive(_targetWindow);
        SendShiftEnterSoftLineBreak();
    }

    internal static void SendShiftEnterSoftLineBreak()
    {
        Win32.WaitForModifiersReleased(500);

        if (ReleaseHeldModifiers())
            Thread.Sleep(1);

        var combo = new Win32.INPUT[4];
        combo[0].type = Win32.INPUT_KEYBOARD; combo[0].u.ki.wVk = Win32.VK_SHIFT;
        combo[1].type = Win32.INPUT_KEYBOARD; combo[1].u.ki.wVk = (ushort)Win32.VK_RETURN;
        combo[2].type = Win32.INPUT_KEYBOARD; combo[2].u.ki.wVk = (ushort)Win32.VK_RETURN; combo[2].u.ki.dwFlags = Win32.KEYEVENTF_KEYUP;
        combo[3].type = Win32.INPUT_KEYBOARD; combo[3].u.ki.wVk = Win32.VK_SHIFT; combo[3].u.ki.dwFlags = Win32.KEYEVENTF_KEYUP;
        Win32.SendInput((uint)combo.Length, combo, Marshal.SizeOf<Win32.INPUT>());
    }

    private static bool ReleaseHeldModifiers()
    {
        var heldKeys = new List<ushort>(6);
        if ((Win32.GetAsyncKeyState(Win32.VK_LSHIFT) & 0x8000) != 0) heldKeys.Add(Win32.VK_LSHIFT);
        if ((Win32.GetAsyncKeyState(Win32.VK_RSHIFT) & 0x8000) != 0) heldKeys.Add(Win32.VK_RSHIFT);
        if ((Win32.GetAsyncKeyState(Win32.VK_CONTROL) & 0x8000) != 0) heldKeys.Add(Win32.VK_CONTROL);
        if ((Win32.GetAsyncKeyState(Win32.VK_MENU) & 0x8000) != 0) heldKeys.Add(Win32.VK_MENU);
        if ((Win32.GetAsyncKeyState(Win32.VK_LWIN) & 0x8000) != 0) heldKeys.Add(Win32.VK_LWIN);
        if ((Win32.GetAsyncKeyState(Win32.VK_RWIN) & 0x8000) != 0) heldKeys.Add(Win32.VK_RWIN);

        if (heldKeys.Count == 0)
            return false;

        var release = new Win32.INPUT[heldKeys.Count];
        for (var i = 0; i < heldKeys.Count; i++)
        {
            release[i].type = Win32.INPUT_KEYBOARD;
            release[i].u.ki.wVk = heldKeys[i];
            release[i].u.ki.dwFlags = Win32.KEYEVENTF_KEYUP;
        }

        Win32.SendInput((uint)release.Length, release, Marshal.SizeOf<Win32.INPUT>());
        return true;
    }

    private static bool SendUnicodeString(string text)
    {
        var buffer = new List<Win32.INPUT>(text.Length * 2);
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];

            // Prefer real Enter key events for line breaks; many editors treat injected '\n'
            // differently from an actual newline command.
            if (c == '\r')
            {
                if (i + 1 < text.Length && text[i + 1] == '\n')
                    i++;
                AddVirtualKeyPress(buffer, (ushort)Win32.VK_RETURN);
                continue;
            }
            if (c == '\n')
            {
                AddVirtualKeyPress(buffer, (ushort)Win32.VK_RETURN);
                continue;
            }
            if (c == '\t')
            {
                AddVirtualKeyPress(buffer, 0x09);
                continue;
            }

            var u = (ushort)c;
            buffer.Add(new Win32.INPUT
            {
                type = Win32.INPUT_KEYBOARD,
                u = new Win32.INPUTUNION
                {
                    ki = new Win32.KEYBDINPUT { wVk = 0, wScan = u, dwFlags = Win32.KEYEVENTF_UNICODE }
                }
            });
            buffer.Add(new Win32.INPUT
            {
                type = Win32.INPUT_KEYBOARD,
                u = new Win32.INPUTUNION
                {
                    ki = new Win32.KEYBDINPUT { wVk = 0, wScan = u, dwFlags = Win32.KEYEVENTF_UNICODE | Win32.KEYEVENTF_KEYUP }
                }
            });
        }

        if (buffer.Count == 0)
            return false;

        var inputs = buffer.ToArray();
        var sent = Win32.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Win32.INPUT>());
        return sent == inputs.Length;
    }

    private static void AddVirtualKeyPress(List<Win32.INPUT> buffer, ushort vk)
    {
        buffer.Add(new Win32.INPUT
        {
            type = Win32.INPUT_KEYBOARD,
            u = new Win32.INPUTUNION
            {
                ki = new Win32.KEYBDINPUT { wVk = vk, wScan = 0, dwFlags = 0 }
            }
        });
        buffer.Add(new Win32.INPUT
        {
            type = Win32.INPUT_KEYBOARD,
            u = new Win32.INPUTUNION
            {
                ki = new Win32.KEYBDINPUT { wVk = vk, wScan = 0, dwFlags = Win32.KEYEVENTF_KEYUP }
            }
        });
    }
}
