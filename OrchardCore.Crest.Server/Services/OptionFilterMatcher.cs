using Crest.Settings;

namespace Crest.Services;

/// <summary>
/// In-memory evaluation of resolved filters against option rows. Providers backed by
/// a database are expected to translate filters into their own query language
/// instead; this is the fallback for small, already-materialized sources such as
/// Content Part Lists, and it defines the operator semantics every provider should match.
/// </summary>
public static class OptionFilterMatcher
{
    public static IReadOnlyList<OptionRow> Apply(IEnumerable<OptionRow> rows, IReadOnlyList<ResolvedOptionFilter>? filters)
    {
        if (filters is null || filters.Count == 0)
        {
            return [.. rows];
        }

        return [.. rows.Where(row => filters.All(filter => Matches(row, filter)))];
    }

    public static bool Matches(OptionRow row, ResolvedOptionFilter filter)
    {
        var actual = Read(row, filter.Path);

        return filter.Operator switch
        {
            OptionFilterOperators.Equals => filter.Values.Any(value => Same(actual, value)),
            OptionFilterOperators.NotEquals => !filter.Values.Any(value => Same(actual, value)),
            OptionFilterOperators.In => filter.Values.Any(value => Same(actual, value)),
            OptionFilterOperators.Contains => actual is not null && filter.Values.Any(value =>
                actual.Contains(value ?? string.Empty, StringComparison.OrdinalIgnoreCase)),
            OptionFilterOperators.GreaterThan => Compare(actual, filter.Values.FirstOrDefault()) > 0,
            OptionFilterOperators.LessThan => Compare(actual, filter.Values.FirstOrDefault()) < 0,
            // An unrecognized operator must not silently drop rows.
            _ => true,
        };
    }

    /// <summary>Searches the configured columns (or every column when none are named)
    /// for the text a user typed.</summary>
    public static IReadOnlyList<OptionRow> Search(IEnumerable<OptionRow> rows, string? searchText, IReadOnlyList<string>? searchColumns)
    {
        if (string.IsNullOrWhiteSpace(searchText))
        {
            return [.. rows];
        }

        var needle = searchText.Trim();

        return
        [
            .. rows.Where(row =>
            {
                var values = searchColumns is { Count: > 0 }
                    ? searchColumns.Select(column => Read(row, column))
                    : row.Values.Values;

                return values.Any(value => value is not null && value.Contains(needle, StringComparison.OrdinalIgnoreCase));
            }),
        ];
    }

    private static string? Read(OptionRow row, string path) =>
        row.Values.TryGetValue(path, out var value) ? value : null;

    private static bool Same(string? actual, string? expected) =>
        string.Equals(actual ?? string.Empty, expected ?? string.Empty, StringComparison.OrdinalIgnoreCase);

    // Numeric when both sides parse as numbers, so quantity/price style filters behave
    // sensibly; otherwise ordinal string comparison.
    private static int Compare(string? actual, string? expected)
    {
        if (decimal.TryParse(actual, out var actualNumber) && decimal.TryParse(expected, out var expectedNumber))
        {
            return actualNumber.CompareTo(expectedNumber);
        }

        return string.Compare(actual, expected, StringComparison.OrdinalIgnoreCase);
    }
}
