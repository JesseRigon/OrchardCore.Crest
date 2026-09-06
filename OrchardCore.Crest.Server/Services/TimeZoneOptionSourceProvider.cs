namespace Crest.Services;

/// <summary>
/// Option source over the platform's time zones (per the standing ruling: time
/// zones are never a seeded list — .NET owns them). The IANA/Windows id is both
/// the row id and the technical key.
/// </summary>
public sealed class TimeZoneOptionSourceProvider : IOptionSourceProvider
{
    public const string ProviderKey = "timezone";

    public string Key => ProviderKey;

    public static class Columns
    {
        public const string Id = "Id";
        public const string DisplayName = "DisplayName";
        public const string Offset = "Offset";
        public const string SupportsDst = "SupportsDst";
    }

    public Task<IReadOnlyList<OptionSourceColumnDescriptor>> DescribeColumnsAsync(string qualifier, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<OptionSourceColumnDescriptor>>(
        [
            new(Columns.DisplayName, "Time zone", IsDefaultDisplay: true),
            new(Columns.Id, "Id"),
            new(Columns.Offset, "UTC offset"),
            new(Columns.SupportsDst, "Daylight saving"),
        ]);

    public Task<IReadOnlyList<OptionRow>> QueryAsync(OptionSourceQuery query, CancellationToken cancellationToken = default)
    {
        // GetSystemTimeZones is already offset-then-name ordered - that IS the
        // natural order a time-zone dropdown wants when no sort is configured.
        var rows = TimeZoneInfo.GetSystemTimeZones().Select(zone => ToRow(zone, query.Columns));
        rows = OptionFilterMatcher.Apply(rows, query.Filters);
        rows = OptionFilterMatcher.Search(rows, query.SearchText,
            query.SearchColumns is { Count: > 0 } ? query.SearchColumns : [Columns.DisplayName, Columns.Id]);
        if (query.SortColumns is { Count: > 0 })
        {
            rows = OptionRowSorter.Apply(rows, query.SortColumns);
        }

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
                rows.Add(ToRow(TimeZoneInfo.FindSystemTimeZoneById(id), columns));
            }
            catch (TimeZoneNotFoundException)
            {
                // Absent, never a hollow row - the stored id no longer resolves here.
            }
        }

        return Task.FromResult<IReadOnlyList<OptionRow>>(rows);
    }

    internal static OptionRow ToRow(TimeZoneInfo zone, IReadOnlyList<string> columns)
    {
        var requested = columns is { Count: > 0 } ? columns : [Columns.DisplayName];
        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var column in requested)
        {
            values[column] = column switch
            {
                Columns.Id => zone.Id,
                Columns.DisplayName => zone.DisplayName,
                Columns.Offset => FormatOffset(zone.BaseUtcOffset),
                Columns.SupportsDst => zone.SupportsDaylightSavingTime ? "true" : "false",
                _ => null,
            };
        }

        return new OptionRow(zone.Id, values) { Key = zone.Id };
    }

    internal static string FormatOffset(TimeSpan offset) =>
        (offset < TimeSpan.Zero ? "-" : "+") + offset.ToString(@"hh\:mm");
}
