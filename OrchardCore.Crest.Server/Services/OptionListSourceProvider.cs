using Crest.Models;

namespace Crest.Services;

/// <summary>
/// Exposes tenant-editable Option Lists as a picker source. The qualifier is the
/// list's logical key ("optionlist:pricing.modifier-kind"), so this one provider
/// serves every list in the tenant.
/// </summary>
public sealed class OptionListSourceProvider(ICrestOptionListService optionLists) : IOptionSourceProvider
{
    public string Key => CrestOptionSourceKeys.OptionListProvider;

    public static class Columns
    {
        /// <summary>The tenant-editable label - the default display column.</summary>
        public const string DisplayText = "DisplayText";

        /// <summary>The technical key code matches on.</summary>
        public const string OptionKey = "Key";

        /// <summary>Module- vs tenant-provided.</summary>
        public const string Source = "Source";
    }

    public Task<IReadOnlyList<OptionSourceColumnDescriptor>> DescribeColumnsAsync(string qualifier, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<OptionSourceColumnDescriptor>>(
        [
            new(Columns.DisplayText, "Label", IsDefaultDisplay: true),
            new(Columns.OptionKey, "Key"),
            new(Columns.Source, "Source"),
        ]);

    public async Task<IReadOnlyList<OptionRow>> QueryAsync(OptionSourceQuery query, CancellationToken cancellationToken = default)
    {
        // Selectable drops hidden options; history still resolves them through
        // GetByIdsAsync below.
        var options = await optionLists.GetSelectableAsync(query.Qualifier, cancellationToken);
        var rows = options.Select(option => ToRow(option, query.Columns));

        rows = OptionFilterMatcher.Apply(rows, query.Filters);
        rows = OptionFilterMatcher.Search(rows, query.SearchText, query.SearchColumns);

        return [.. rows.Skip(query.Skip).Take(query.Take)];
    }

    public async Task<IReadOnlyList<OptionRow>> GetByIdsAsync(string qualifier, IReadOnlyList<string> ids, IReadOnlyList<string> columns, CancellationToken cancellationToken = default)
    {
        if (ids.Count == 0)
        {
            return [];
        }

        // Reads the whole set once - it is a small, per-request memoized content item -
        // rather than one lookup per id.
        var list = await optionLists.GetAsync(qualifier, cancellationToken);
        if (list is null)
        {
            return [];
        }

        var wanted = new HashSet<string>(ids, StringComparer.OrdinalIgnoreCase);

        return [.. list.Options.Where(option => wanted.Contains(option.ContentItemId)).Select(option => ToRow(option, columns))];
    }

    private static OptionRow ToRow(CrestOptionModel option, IReadOnlyList<string> columns)
    {
        var requested = columns is { Count: > 0 } ? columns : [Columns.DisplayText];
        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        foreach (var column in requested)
        {
            values[column] = column switch
            {
                Columns.OptionKey => option.Key,
                Columns.Source => option.Source,
                Columns.DisplayText => option.DisplayText,
                _ => null,
            };
        }

        return new OptionRow(option.ContentItemId, values)
        {
            Key = option.Key,
            Hidden = option.Hidden,
        };
    }
}
