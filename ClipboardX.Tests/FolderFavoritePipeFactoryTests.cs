using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using ClipboardManager;

namespace ClipboardX.Tests;

public sealed class FolderFavoritePipeFactoryTests
{
    [Fact]
    public void ClientOptions_DoNotRequireMatchingElevationLevel()
    {
        Assert.Equal(
            (PipeOptions)0,
            FolderFavoritePipeFactory.ClientOptions & PipeOptions.CurrentUserOnly);
        Assert.NotEqual(
            (PipeOptions)0,
            FolderFavoritePipeFactory.ClientOptions & PipeOptions.Asynchronous);
    }

    [Fact]
    public void CreateSecurity_OnlyAllowsSpecifiedUser()
    {
        var userSid = new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null);

        var security = FolderFavoritePipeFactory.CreateSecurity(userSid);
        var rules = security.GetAccessRules(
                includeExplicit: true,
                includeInherited: false,
                typeof(SecurityIdentifier))
            .Cast<PipeAccessRule>()
            .ToList();

        Assert.True(security.AreAccessRulesProtected);
        var rule = Assert.Single(rules);
        Assert.Equal(userSid, rule.IdentityReference);
        Assert.Equal(AccessControlType.Allow, rule.AccessControlType);
        Assert.Equal(PipeAccessRights.FullControl, rule.PipeAccessRights);
    }

    [Fact]
    public async Task CreateServer_AcceptsClientWithoutCurrentUserOnly()
    {
        await using var server = FolderFavoritePipeFactory.CreateServer();
        using var client = new NamedPipeClientStream(
            ".",
            FolderFavoriteCommand.PipeName,
            PipeDirection.InOut,
            FolderFavoritePipeFactory.ClientOptions);

        var accepting = server.WaitForConnectionAsync();
        await client.ConnectAsync(2_000);
        await accepting;

        Assert.True(server.IsConnected);
        Assert.True(client.IsConnected);
    }
}
