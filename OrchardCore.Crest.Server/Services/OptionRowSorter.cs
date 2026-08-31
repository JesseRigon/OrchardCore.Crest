namespace Crest.Services;

/// <summary>
/// In-memory, culture-aware ordering of option rows by the INSTANCE's requested sort
/// columns. Sort is an instance parameter - the same source renders category-grouped
/// in one picker and key-ordered in another - so it applies to every provider,
/// foreign-key sources included, never to the source's own data. Multi-column: the
/// first column is the primary sort, later ones break ties (grouping by category =
/// ["Category", "DisplayText"]). Collation uses the CURRENT culture (the request's
/// resolved culture, set by DisplayManager), so ordering follows the reader's
/// language. Rows must be fully materialized before this runs - sorting a paged
/// subset would only sort within the page.
/// </summary>
public static class OptionRowSorter
{
    public static IReadOnlyList<OptionRow> Apply(IEnumerable<OptionRow> rows, IReadOnlyList<string>? sortColumns)
    {
        if (sortColumns is null || sortColumns.Count == 0)
        {
            return [.. rows];
        }

        var collator = StringComparer.CurrentCultureIgnoreCase;
        var ordered = rows.OrderBy(row => Read(row, sortColumns[0]), collator);
        for (var index = 1; index < sortColumns.Count; index++)
        {
            var column = sortColumns[index];
            ordered = ordered.ThenBy(row => Read(row, column), collator);
        }

        return [.. ordered];
    }

    private static string Read(OptionRow row, string column) =>
        row.Values.TryGetValue(column, out var value) ? value ?? string.Empty : string.Empty;
}
