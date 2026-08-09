using System.Windows.Input;
using System.Windows.Interop;

namespace ClipboardManager;

public readonly record struct HotkeyRecordingResult(uint Modifiers, uint Key);

public static class HotkeyRecordingController
{
    public static bool TryRecord(
        Key key,
        Key systemKey,
        uint modifiers,
        out HotkeyRecordingResult result,
        bool allowNoModifiers = false)
    {
        result = default;
        var normalizedKey = key == Key.System ? systemKey : key;
        if (IsModifierOnlyKey(normalizedKey) || (!allowNoModifiers && modifiers == 0))
            return false;

        result = new HotkeyRecordingResult(modifiers, (uint)KeyInterop.VirtualKeyFromKey(normalizedKey));
        return true;
    }

    public static bool IsModifierOnlyKey(Key key) =>
        key is Key.LeftCtrl or Key.RightCtrl
            or Key.LeftShift or Key.RightShift
            or Key.LeftAlt or Key.RightAlt
            or Key.LWin or Key.RWin;
}
