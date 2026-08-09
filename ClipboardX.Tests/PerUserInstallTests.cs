using ClipboardManager;

namespace ClipboardX.Tests;

public sealed class PerUserInstallTests
{
    [Theory]
    [InlineData("--uninstall")]
    [InlineData("--UNINSTALL")]
    [InlineData("/uninstall")]
    [InlineData("/UNINSTALL")]
    public void HasUninstallArgument_AcceptsSupportedForms(string argument)
    {
        Assert.True(PerUserInstall.HasUninstallArgument(["--other", argument]));
    }

    [Theory]
    [InlineData("")]
    [InlineData("uninstall")]
    [InlineData("--uninstall-now")]
    [InlineData("/uninstall-now")]
    public void HasUninstallArgument_RejectsUnsupportedForms(string argument)
    {
        Assert.False(PerUserInstall.HasUninstallArgument([argument]));
    }

    [Theory]
    [InlineData("ClipboardX.exe", true)]
    [InlineData("ClipboardX.DLL", true)]
    [InlineData("ClipboardX.runtimeconfig.json", true)]
    [InlineData("ClipboardX.pdb", false)]
    [InlineData("readme.txt", false)]
    [InlineData("extensionless", false)]
    public void ShouldCopyDeploymentFile_OnlyAcceptsRuntimeFiles(string fileName, bool expected)
    {
        Assert.Equal(expected, PerUserInstall.ShouldCopyDeploymentFile(fileName));
    }
}
