namespace ClipboardManager;

internal static class EverythingFolderQueryBuilder
{
    internal static string Build(SearchQuerySpec spec)
    {
        var clauses = new List<string>();

        if (spec.AnchorStart && !string.IsNullOrWhiteSpace(spec.StartNeedle))
        {
            var start = spec.StartNeedle;
            if (start.Length == 1 && char.IsLetter(start[0]))
                clauses.Add($"{start}:");
            else
                clauses.Add($"startwith:{QuoteTerm(start)}");
        }

        if (spec.AnchorEnd && !string.IsNullOrWhiteSpace(spec.EndNeedle))
            clauses.Add($"endwith:{QuoteTerm(spec.EndNeedle)}");

        foreach (var token in spec.ContainsTokens)
            clauses.Add(QuoteTerm(token));

        var combined = string.Join(" ", clauses.Distinct(StringComparer.OrdinalIgnoreCase));
        return string.IsNullOrWhiteSpace(combined) ? "folder:" : "folder: " + combined;
    }

    internal static int CandidateQueryMax(int maxResults)
    {
        var expanded = Math.Max((long)maxResults * 10, 500L);
        return (int)Math.Clamp(expanded, 1L, 5_000L);
    }

    private static string QuoteTerm(string term) =>
        "\"" + term.Replace("\"", "\\\"") + "\"";
}
