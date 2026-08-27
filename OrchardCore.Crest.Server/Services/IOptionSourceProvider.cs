using Crest.Settings;

namespace Crest.Services;

/// <summary>
/// A source an <see cref="Crest.Fields.OptionPickerField"/> can draw from. Providers
/// keep the field itself ignorant of users, items or option lists, the same way
/// Crest.Icons hosts icon providers.
/// </summary>
public interface IOptionSourceProvider
{
    /// <summary>Provider key matched against the first segment of a source key
    /// ("optionlist" in "optionlist:pricing.side").</summary>
    string Key { get; }

    /// <summary>
    /// The columns this source can offer for the given qualifier - drives the column
    /// picker in the field's settings editor, so paths never have to be typed blind.
    /// </summary>
    Task<IReadOnlyList<OptionSourceColumnDescriptor>> DescribeColumnsAsync(string qualifier, CancellationToken cancellationToken = default);

    /// <summary>Rows to offer in the dropdown, filtered and paged.</summary>
    Task<IReadOnlyList<OptionRow>> QueryAsync(OptionSourceQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Rows for already-selected ids, so stored values can be rendered. BATCHED by
    /// design: rendering a 500-line document must be one call per source, not one per
    /// line.
    /// </summary>
    Task<IReadOnlyList<OptionRow>> GetByIdsAsync(string qualifier, IReadOnlyList<string> ids, IReadOnlyList<string> columns, CancellationToken cancellationToken = default);
}

/// <summary>What a provider can display for a source.</summary>
public sealed record OptionSourceColumnDescriptor(string Path, string Label, bool IsDefaultDisplay = false);

/// <summary>One row of a source: an id plus the requested columns.</summary>
public sealed record OptionRow(string Id, IReadOnlyDictionary<string, string?> Values)
{
    /// <summary>The technical key when the source has one distinct from the id
    /// (Option Lists do; entity sources like users do not - their id IS the key).</summary>
    public string? Key { get; init; }

    /// <summary>Set for options a tenant hid: still resolvable for history, but not
    /// offered in pickers.</summary>
    public bool Hidden { get; init; }

    // NOTE: there is deliberately no "redacted" row shape. A record the caller may not
    // see is ABSENT from the result rather than returned hollow - a placeholder row
    // still asserts that the reference resolves, which a caller can act on. The
    // referencing item is then out of scope too (OptionSourceReferenceGuard).
}

/// <summary>A dropdown query: what to search, restrict, sort and return.</summary>
public sealed record OptionSourceQuery(
    string Qualifier,
    IReadOnlyList<string> Columns,
    string? SearchText = null,
    IReadOnlyList<ResolvedOptionFilter>? Filters = null,
    IReadOnlyList<string>? SearchColumns = null,
    IReadOnlyList<string>? SortColumns = null,
    int Skip = 0,
    int Take = 50);

/// <summary>
/// A filter with its comparison value already resolved - literal values pass straight
/// through, while <see cref="OptionFilter.ValueFrom"/> has been read from the item
/// being edited before the provider ever sees it.
/// </summary>
public sealed record ResolvedOptionFilter(string Path, string Operator, IReadOnlyList<string> Values)
{
    public static ResolvedOptionFilter From(OptionFilter filter, IReadOnlyList<string> values) =>
        new(filter.Path, filter.Operator, values);
}
