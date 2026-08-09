using System.Runtime.InteropServices;

namespace ClipboardManager;

internal static partial class FileDialogJumpHelper
{
    private static void SendF4()
    {
        const ushort virtualKey = 0x73;
        var inputs = new Win32.INPUT[2];
        inputs[0].type = Win32.INPUT_KEYBOARD;
        inputs[0].u.ki.wVk = virtualKey;
        inputs[1].type = Win32.INPUT_KEYBOARD;
        inputs[1].u.ki.wVk = virtualKey;
        inputs[1].u.ki.dwFlags = Win32.KEYEVENTF_KEYUP;
        Win32.SendInput(2, inputs, Marshal.SizeOf<Win32.INPUT>());
    }

    private static void SendAltN()
    {
        const ushort virtualKeyMenu = 0x12;
        const ushort virtualKeyN = 0x4E;
        var inputs = new Win32.INPUT[4];
        inputs[0].type = Win32.INPUT_KEYBOARD;
        inputs[0].u.ki.wVk = virtualKeyMenu;
        inputs[1].type = Win32.INPUT_KEYBOARD;
        inputs[1].u.ki.wVk = virtualKeyN;
        inputs[2].type = Win32.INPUT_KEYBOARD;
        inputs[2].u.ki.wVk = virtualKeyN;
        inputs[2].u.ki.dwFlags = Win32.KEYEVENTF_KEYUP;
        inputs[3].type = Win32.INPUT_KEYBOARD;
        inputs[3].u.ki.wVk = virtualKeyMenu;
        inputs[3].u.ki.dwFlags = Win32.KEYEVENTF_KEYUP;
        Win32.SendInput(4, inputs, Marshal.SizeOf<Win32.INPUT>());
    }

    private static void SendAltD()
    {
        var inputs = new Win32.INPUT[4];
        inputs[0].type = Win32.INPUT_KEYBOARD;
        inputs[0].u.ki.wVk = 0x12;
        inputs[1].type = Win32.INPUT_KEYBOARD;
        inputs[1].u.ki.wVk = 0x44;
        inputs[2].type = Win32.INPUT_KEYBOARD;
        inputs[2].u.ki.wVk = 0x44;
        inputs[2].u.ki.dwFlags = Win32.KEYEVENTF_KEYUP;
        inputs[3].type = Win32.INPUT_KEYBOARD;
        inputs[3].u.ki.wVk = 0x12;
        inputs[3].u.ki.dwFlags = Win32.KEYEVENTF_KEYUP;
        Win32.SendInput(4, inputs, Marshal.SizeOf<Win32.INPUT>());
    }

    private static void SendCtrlL()
    {
        var inputs = new Win32.INPUT[4];
        inputs[0].type = Win32.INPUT_KEYBOARD;
        inputs[0].u.ki.wVk = Win32.VK_CONTROL;
        inputs[1].type = Win32.INPUT_KEYBOARD;
        inputs[1].u.ki.wVk = Win32.VK_L;
        inputs[2].type = Win32.INPUT_KEYBOARD;
        inputs[2].u.ki.wVk = Win32.VK_L;
        inputs[2].u.ki.dwFlags = Win32.KEYEVENTF_KEYUP;
        inputs[3].type = Win32.INPUT_KEYBOARD;
        inputs[3].u.ki.wVk = Win32.VK_CONTROL;
        inputs[3].u.ki.dwFlags = Win32.KEYEVENTF_KEYUP;
        Win32.SendInput(4, inputs, Marshal.SizeOf<Win32.INPUT>());
    }

    private static void SendCtrlA()
    {
        var inputs = new Win32.INPUT[4];
        inputs[0].type = Win32.INPUT_KEYBOARD;
        inputs[0].u.ki.wVk = Win32.VK_CONTROL;
        inputs[1].type = Win32.INPUT_KEYBOARD;
        inputs[1].u.ki.wVk = Win32.VK_A;
        inputs[2].type = Win32.INPUT_KEYBOARD;
        inputs[2].u.ki.wVk = Win32.VK_A;
        inputs[2].u.ki.dwFlags = Win32.KEYEVENTF_KEYUP;
        inputs[3].type = Win32.INPUT_KEYBOARD;
        inputs[3].u.ki.wVk = Win32.VK_CONTROL;
        inputs[3].u.ki.dwFlags = Win32.KEYEVENTF_KEYUP;
        Win32.SendInput(4, inputs, Marshal.SizeOf<Win32.INPUT>());
    }

    private static void SendEnter()
    {
        var inputs = new Win32.INPUT[2];
        inputs[0].type = Win32.INPUT_KEYBOARD;
        inputs[0].u.ki.wVk = (ushort)Win32.VK_RETURN;
        inputs[1].type = Win32.INPUT_KEYBOARD;
        inputs[1].u.ki.wVk = (ushort)Win32.VK_RETURN;
        inputs[1].u.ki.dwFlags = Win32.KEYEVENTF_KEYUP;
        Win32.SendInput(2, inputs, Marshal.SizeOf<Win32.INPUT>());
    }

    private static void SendUnicodeString(string value)
    {
        var inputs = new List<Win32.INPUT>(value.Length * 2);
        foreach (var character in value)
        {
            var scanCode = (ushort)character;
            inputs.Add(new Win32.INPUT
            {
                type = Win32.INPUT_KEYBOARD,
                u = new Win32.INPUTUNION
                {
                    ki = new Win32.KEYBDINPUT
                    {
                        wVk = 0,
                        wScan = scanCode,
                        dwFlags = Win32.KEYEVENTF_UNICODE,
                        time = 0,
                        dwExtraInfo = IntPtr.Zero,
                    },
                },
            });
            inputs.Add(new Win32.INPUT
            {
                type = Win32.INPUT_KEYBOARD,
                u = new Win32.INPUTUNION
                {
                    ki = new Win32.KEYBDINPUT
                    {
                        wVk = 0,
                        wScan = scanCode,
                        dwFlags = Win32.KEYEVENTF_UNICODE | Win32.KEYEVENTF_KEYUP,
                        time = 0,
                        dwExtraInfo = IntPtr.Zero,
                    },
                },
            });
        }
        var inputArray = inputs.ToArray();
        Win32.SendInput((uint)inputArray.Length, inputArray, Marshal.SizeOf<Win32.INPUT>());
    }
}
