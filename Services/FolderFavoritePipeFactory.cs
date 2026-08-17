using System.IO;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;

namespace ClipboardManager;

internal static class FolderFavoritePipeFactory
{
    internal const PipeOptions ClientOptions = PipeOptions.Asynchronous;

    internal static NamedPipeServerStream CreateServer()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var userSid = identity.User
            ?? throw new InvalidOperationException("Cannot determine the current Windows user SID.");

        return NamedPipeServerStreamAcl.Create(
            FolderFavoriteCommand.PipeName,
            PipeDirection.InOut,
            1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous | PipeOptions.FirstPipeInstance,
            0,
            0,
            CreateSecurity(userSid),
            HandleInheritability.None,
            (PipeAccessRights)0);
    }

    internal static PipeSecurity CreateSecurity(SecurityIdentifier userSid)
    {
        ArgumentNullException.ThrowIfNull(userSid);

        var security = new PipeSecurity();
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        security.SetOwner(userSid);
        security.AddAccessRule(new PipeAccessRule(
            userSid,
            PipeAccessRights.FullControl,
            AccessControlType.Allow));
        return security;
    }
}
