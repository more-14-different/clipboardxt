using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace ClipboardManager;

internal static partial class EverythingIpc
{
    static EverythingIpc()
    {
        NativeLibrary.SetDllImportResolver(typeof(EverythingIpc).Assembly, ResolveEverything64Module);
    }

    private static IntPtr ResolveEverything64Module(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (!string.Equals(libraryName, "Everything64.dll", StringComparison.OrdinalIgnoreCase))
            return IntPtr.Zero;

        foreach (var dir in CandidateProbeDirectories())
        {
            var path = Path.Combine(dir, Environment.Is64BitProcess ? "Everything64.dll" : "Everything32.dll");
            try
            {
                if (File.Exists(path))
                    return NativeLibrary.Load(path);
            }
            catch
            {
                /* try next */
            }
        }

        return IntPtr.Zero;
    }

    private static IEnumerable<string> CandidateProbeDirectories()
    {
        var location = typeof(EverythingIpc).Assembly.Location;
        if (!string.IsNullOrEmpty(location))
        {
            var directory = Path.GetDirectoryName(location);
            if (!string.IsNullOrEmpty(directory))
            {
                var root = directory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                yield return root;
                yield return Path.Combine(root, "native");
            }
        }

        var baseDirectory = AppContext.BaseDirectory;
        if (!string.IsNullOrEmpty(baseDirectory))
        {
            var root = baseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            yield return root;
            yield return Path.Combine(root, "native");
        }

        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        if (!string.IsNullOrEmpty(programFiles))
            yield return Path.Combine(programFiles, "Everything");

        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        if (!string.IsNullOrEmpty(programFilesX86))
            yield return Path.Combine(programFilesX86, "Everything");

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrEmpty(localAppData))
            yield return Path.Combine(localAppData, "Programs", "Everything");

        foreach (var directory in ProbePathEnvironment())
            yield return directory;

        foreach (var directory in CollectRegistryEverythingInstallDirs())
            yield return directory;
    }

    private static IEnumerable<string> ProbePathEnvironment()
    {
        var path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(path)) yield break;
        foreach (var segment in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var directory = segment.Trim().Trim('"');
            if (directory.Length > 0 && Directory.Exists(directory))
                yield return directory;
        }
    }

    private static List<string> CollectRegistryEverythingInstallDirs()
    {
        var directories = new List<string>(4);
        foreach (var root in new[] { Registry.LocalMachine, Registry.CurrentUser })
        {
            foreach (var subKey in new[]
                     {
                         @"SOFTWARE\voidtools\Everything",
                         @"SOFTWARE\WOW6432Node\voidtools\Everything",
                     })
            {
                try
                {
                    using var key = root.OpenSubKey(subKey);
                    if (key == null) continue;
                    foreach (var valueName in new[] { "exe_path", "install_path", "path" })
                    {
                        var value = key.GetValue(valueName) as string;
                        if (string.IsNullOrWhiteSpace(value)) continue;
                        value = value.Trim().Trim('"');
                        string? directory = null;
                        if (value.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                            directory = Path.GetDirectoryName(value);
                        else if (Directory.Exists(value))
                            directory = value;

                        if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
                        {
                            directories.Add(directory);
                            break;
                        }
                    }
                }
                catch
                {
                    /* ignore */
                }
            }
        }

        return directories;
    }
}
