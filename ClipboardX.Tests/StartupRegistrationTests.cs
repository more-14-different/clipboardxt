using ClipboardManager;

namespace ClipboardX.Tests;

public sealed class StartupRegistrationTests
{
    [Fact]
    public void ManagesStartupRegistration_OnlyInReleaseBuilds()
    {
#if DEBUG
        Assert.False(StartupRegistration.ManagesStartupRegistration);
#else
        Assert.True(StartupRegistration.ManagesStartupRegistration);
#endif
    }

    [Fact]
    public void BuildScheduledTaskCreateArguments_UsesHighestOnLogonForExplicitUser()
    {
        var arguments = StartupRegistration.BuildScheduledTaskCreateArguments(
            @"C:\Program Files\ClipboardXt\ClipboardX.exe",
            @"WORKSTATION\Test User");

        Assert.Equal(
            new[]
            {
                "/Create",
                "/F",
                "/TN",
                StartupRegistration.ScheduledTaskName,
                "/TR",
                "\"C:\\Program Files\\ClipboardXt\\ClipboardX.exe\"",
                "/SC",
                "ONLOGON",
                "/RL",
                "HIGHEST",
                "/RU",
                @"WORKSTATION\Test User"
            },
            arguments);
    }

    [Fact]
    public void BuildScheduledTaskCreateArguments_OmitsUserForFallback()
    {
        var arguments = StartupRegistration.BuildScheduledTaskCreateArguments(
            @"C:\ClipboardXt\ClipboardX.exe",
            null);

        Assert.DoesNotContain("/RU", arguments);
        Assert.Equal("HIGHEST", arguments[^1]);
    }

    [Fact]
    public void BuildScheduledTaskDeleteArguments_DeletesExpectedTask()
    {
        Assert.Equal(
            new[] { "/Delete", "/F", "/TN", StartupRegistration.ScheduledTaskName },
            StartupRegistration.BuildScheduledTaskDeleteArguments());
    }

    [Fact]
    public void BuildScheduledTaskQueryArguments_QueriesExpectedTask()
    {
        Assert.Equal(
            new[] { "/Query", "/TN", StartupRegistration.ScheduledTaskName },
            StartupRegistration.BuildScheduledTaskQueryArguments());
    }
}
