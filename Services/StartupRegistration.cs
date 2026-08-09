using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace ClipboardManager;

/// <summary>
/// Registers the application for the current user's Windows logon.
/// Normal startup uses HKCU Run; elevated startup uses a highest-privilege
/// scheduled task so logon does not depend on an interactive UAC prompt.
/// </summary>
public static class StartupRegistration
{
    private const string RunSubKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string LegacyValueName = "ClipboardManager";
    private const string ValueName = "ClipboardX";
    internal const string ScheduledTaskName = "ClipboardX_AutoStart";

    /// <summary>
    /// Debug builds are launched and registered by developer-owned tooling/tasks.
    /// Only Release builds may mutate the application-owned startup registration.
    /// </summary>
    internal static bool ManagesStartupRegistration
    {
        get
        {
#if DEBUG
            return false;
#else
            return true;
#endif
        }
    }

    private static string? ResolveExecutablePathForStartup()
    {
        var processPath = Environment.ProcessPath;
        if (string.IsNullOrEmpty(processPath)) return null;

        // Do not replace a real installed startup registration while running
        // under dotnet run or another managed host.
        if (processPath.EndsWith("dotnet.exe", StringComparison.OrdinalIgnoreCase))
            return null;

        return File.Exists(processPath)
               && processPath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? processPath
            : null;
    }

    /// <param name="runAtStartup">Whether the app should start at user logon.</param>
    /// <param name="runAsAdministrator">
    /// When true, use a highest-privilege scheduled task without a logon UAC prompt;
    /// otherwise use HKCU Run.
    /// </param>
    public static void Apply(bool runAtStartup, bool runAsAdministrator)
    {
        if (!ManagesStartupRegistration) return;

        if (!runAtStartup)
        {
            RemoveRunKeyEntry();
            RemoveScheduledTask();
            return;
        }

        var exePath = ResolveExecutablePathForStartup();
        if (string.IsNullOrEmpty(exePath)) return;

        if (runAsAdministrator)
        {
            RemoveRunKeyEntry();
            RegisterScheduledTaskForElevatedStartup(exePath);
        }
        else
        {
            RemoveScheduledTask();
            RegisterRunKeyEntry(exePath);
        }
    }

    public static bool IsRegistered() =>
        ManagesStartupRegistration
        && (IsRunKeyRegistered() || IsScheduledTaskRegistered());

    private static bool IsRunKeyRegistered()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunSubKey, writable: false);
            return key?.GetValue(ValueName) != null
                   || key?.GetValue(LegacyValueName) != null;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsScheduledTaskRegistered() =>
        RunSchtasks(BuildScheduledTaskQueryArguments(), 3000, out _) == 0;

    private static void RegisterRunKeyEntry(string exePath)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunSubKey, writable: true);
            if (key == null) return;
            key.DeleteValue(LegacyValueName, throwOnMissingValue: false);
            key.SetValue(ValueName, $"\"{exePath}\"");
        }
        catch
        {
            // Registry access can be blocked by policy; startup registration is best effort.
        }
    }

    private static void RemoveRunKeyEntry()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunSubKey, writable: true);
            if (key == null) return;
            key.DeleteValue(ValueName, throwOnMissingValue: false);
            key.DeleteValue(LegacyValueName, throwOnMissingValue: false);
        }
        catch
        {
            // Best-effort cleanup.
        }
    }

    private static void RegisterScheduledTaskForElevatedStartup(string exePath)
    {
        var userIdentity = $"{Environment.UserDomainName}\\{Environment.UserName}";
        var arguments = BuildScheduledTaskCreateArguments(exePath, userIdentity);
        if (RunSchtasks(arguments, 8000, out _) == 0) return;

        // Some Windows/domain configurations reject an explicit /RU. Let
        // schtasks use the current caller as a compatibility fallback.
        RunSchtasks(BuildScheduledTaskCreateArguments(exePath, null), 8000, out _);
    }

    private static void RemoveScheduledTask() =>
        RunSchtasks(BuildScheduledTaskDeleteArguments(), 8000, out _);

    internal static IReadOnlyList<string> BuildScheduledTaskCreateArguments(
        string exePath,
        string? userIdentity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(exePath);

        var arguments = new List<string>
        {
            "/Create",
            "/F",
            "/TN",
            ScheduledTaskName,
            "/TR",
            $"\"{exePath}\"",
            "/SC",
            "ONLOGON",
            "/RL",
            "HIGHEST"
        };

        if (!string.IsNullOrWhiteSpace(userIdentity))
        {
            arguments.Add("/RU");
            arguments.Add(userIdentity);
        }

        return arguments;
    }

    internal static IReadOnlyList<string> BuildScheduledTaskDeleteArguments() =>
        ["/Delete", "/F", "/TN", ScheduledTaskName];

    internal static IReadOnlyList<string> BuildScheduledTaskQueryArguments() =>
        ["/Query", "/TN", ScheduledTaskName];

    private static int RunSchtasks(
        IReadOnlyList<string> arguments,
        int timeoutMs,
        out string stderr)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "schtasks.exe",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            foreach (var argument in arguments)
                startInfo.ArgumentList.Add(argument);

            using var process = Process.Start(startInfo);
            if (process == null)
            {
                stderr = "";
                return -1;
            }

            if (!process.WaitForExit(timeoutMs))
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit(1000);
                }
                catch
                {
                    // Best-effort timeout cleanup.
                }

                stderr = "schtasks timed out";
                return -1;
            }

            stderr = process.StandardError.ReadToEnd();
            return process.ExitCode;
        }
        catch
        {
            stderr = "";
            return -1;
        }
    }
}
