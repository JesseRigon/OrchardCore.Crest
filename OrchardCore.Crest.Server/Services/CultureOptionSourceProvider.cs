using System.Globalization;

namespace Crest.Services;

/// <summary>
/// Option source over the platform's cultures (per the standing ruling: cultures
/// are never a seeded list — .NET/Orchard own them). The culture name ("en-US")
/// is both the row id and the technical key. The qualifier narrows the set:
/// blank/"specific" = specific cultures (the usual pick-a-locale dropdown),
/// "neutral" = neutral cultures, "all" = both.
/// </summary>
public sealed class CultureOptionSourceProvider : IOptionSourceProvider
{
    public const string ProviderKey = "culture";

    public string Key => ProviderKey;

    public static class Columns
    {
        public const string Name = "Name";
        public const string EnglishName = "EnglishName";
        public const string NativeName = "NativeName";
        public const string TwoLetterIso = "TwoLetterIso";
    }

    public Task<IReadOnlyList<OptionSourceColumnDescriptor>> DescribeColumnsAsync(string qualifier, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<OptionSourceColumnDescriptor>>(
        [
            new(Columns.EnglishName, "Culture", IsDefaultDisplay: true),
            new(Columns.Name, "Code"),
            new(Columns.NativeName, "Native name"),
            new(Columns.TwoLetterIso, "ISO 639-1"),
        ]);

    public Task<IReadOnlyList<OptionRow>> QueryAsync(OptionSourceQuery query, CancellationToken cancellationToken = default)
    {
        var rows = Cultures(query.Qualifier).Select(culture => ToRow(culture, query.Columns));
        rows = OptionFilterMatcher.Apply(rows, query.Filters);
        rows = OptionFilterMatcher.Search(rows, query.SearchText,
            query.SearchColumns is { Count: > 0 } ? query.SearchColumns : [Columns.Name, Columns.EnglishName]);
        rows = query.SortColumns is { Count: > 0 }
            ? OptionRowSorter.Apply(rows, query.SortColumns)
            : rows.OrderBy(row => row.Values.TryGetValue(Columns.EnglishName, out var name) ? name : row.Id, StringComparer.OrdinalIgnoreCase);
        return Task.FromResult<IReadOnlyList<OptionRow>>([.. rows.Skip(query.Skip).Take(query.Take)]);
    }

    public Task<IReadOnlyList<OptionRow>> GetByIdsAsync(string qualifier, IReadOnlyList<string> ids, IReadOnlyList<string> columns, CancellationToken cancellationToken = default)
    {
        if (ids.Count == 0)
        {
            return Task.FromResult<IReadOnlyList<OptionRow>>([]);
        }

        var rows = new List<OptionRow>(ids.Count);
        foreach (var id in ids.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                var culture = CultureInfo.GetCultureInfo(id);
                // GetCultureInfo fabricates a culture for any well-formed name;
                // only offer ones the platform actually knows.
                if (!string.IsNullOrEmpty(culture.Name))
                {
                    rows.Add(ToRow(culture, columns));
                }
            }
            catch (CultureNotFoundException)
            {
                // Absent, never a hollow row.
            }
        }

        return Task.FromResult<IReadOnlyList<OptionRow>>(rows);
    }

    internal static IEnumerable<CultureInfo> Cultures(string? qualifier)
    {
        var types = qualifier?.Trim().ToLowerInvariant() switch
        {
            "neutral" => CultureTypes.NeutralCultures,
            "all" => CultureTypes.NeutralCultures | CultureTypes.SpecificCultures,
            _ => CultureTypes.SpecificCultures,
        };

        // The invariant culture has an empty name - it is not a pickable locale.
        return CultureInfo.GetCultures(types).Where(culture => !string.IsNullOrEmpty(culture.Name));
    }

    internal static OptionRow ToRow(CultureInfo culture, IReadOnlyList<string> columns)
    {
        var requested = columns is { Count: > 0 } ? columns : [Columns.EnglishName];
        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var column in requested)
        {
            values[column] = column switch
            {
                Columns.Name => culture.Name,
                Columns.EnglishName => culture.EnglishName,
                Columns.NativeName => culture.NativeName,
                Columns.TwoLetterIso => culture.TwoLetterISOLanguageName,
                _ => null,
            };
        }

        return new OptionRow(culture.Name, values) { Key = culture.Name };
    }
}
