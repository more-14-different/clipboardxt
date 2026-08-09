using ClipboardManager;

namespace ClipboardX.Tests;

public sealed class FileDialogJumpPickerHookLifetimeTests
{
    [Fact]
    public void TryClaimOwnerDestroyHook_OnlyFirstMatchingCallbackClaimsHandle()
    {
        var callbackHook = new IntPtr(1234);
        var registeredHook = callbackHook;

        Assert.True(FileDialogJumpPickerWindow.TryClaimOwnerDestroyHook(
            ref registeredHook,
            callbackHook));
        Assert.Equal(IntPtr.Zero, registeredHook);
        Assert.False(FileDialogJumpPickerWindow.TryClaimOwnerDestroyHook(
            ref registeredHook,
            callbackHook));
    }

    [Fact]
    public void TryClaimOwnerDestroyHook_DoesNotClearNewerRegistration()
    {
        var staleCallbackHook = new IntPtr(1234);
        var currentHook = new IntPtr(5678);
        var registeredHook = currentHook;

        Assert.False(FileDialogJumpPickerWindow.TryClaimOwnerDestroyHook(
            ref registeredHook,
            staleCallbackHook));
        Assert.Equal(currentHook, registeredHook);
    }
}
