using System.Diagnostics;

namespace ClipboardManager;

public static class ProcessNameCatalog
{
    public static IReadOnlyList<string> GetRunningProcessNames(int selfProcessId) =>
        Process.GetProcesses()
            .Where(p =>
            {
                try
                {
                    return p.Id != selfProcessId && p.Id > 4 && !string.IsNullOrEmpty(p.ProcessName);
                }
                catch
                {
                    return false;
                }
            })
            .Select(p =>
            {
                try
                {
                    return p.ProcessName;
                }
                catch
                {
                    return "";
                }
            })
            .Where(n => !string.IsNullOrEmpty(n))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToList();

    public static IReadOnlyList<string> Filter(IEnumerable<string> processNames, string? filter)
    {
        var query = string.IsNullOrWhiteSpace(filter)
            ? processNames
            : processNames.Where(n => n.Contains(filter.Trim(), StringComparison.OrdinalIgnoreCase));

        return query.ToList();
    }
}
