using System.Text.Json.Dynamic;
using System.Text.Json.Nodes;
using Crest.Settings;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Records;
using YesSql;
using YesSql.Services;

namespace Crest.Services;

/// <summary>
/// Exposes content items of a given type as a picker source: "contentitem:Customer".
/// This is the source behind entity dropdowns (customers, vendors, sites) as opposed
/// to the fixed named choices an Option List holds.
/// </summary>
/// <remarks>
/// <para>
/// The reason this exists separately from <see cref="UserOptionSourceProvider"/> is
/// PUSHDOWN. The user provider materializes its rows and matches them in memory, which
/// is fine for a tenant's user table and completely wrong for a customer table with a
/// million rows: "show only active customers" must not mean "load a million customers
/// and throw most of them away".
/// </para>
/// <para>
/// So filters here are split. Anything expressible against the content item index
/// (<see cref="OptionRowPaths"/>) becomes part of the SQL query. Anything addressing a
/// value inside the item's own document is matched afterwards, against the already
/// narrowed set. <see cref="Pushdown"/> is the pure part of that decision and is unit
/// tested directly, because a filter silently falling back to in-memory matching is
/// exactly the kind of regression that is invisible until a table gets large.
/// </para>
/// <para>
/// NOTE: none of this is authorization. These filters say what the DROPDOWN offers,
/// not what the USER may see - see the 6d data-scope work.
/// </para>
/// </remarks>
public sealed class ContentItemOptionSourceProvider(
    ISession session,
    IOptionSourceScopeResolver scopeResolver) : IOptionSourceProvider
{
    public const string ProviderKey = "contentitem";

    public string Key => ProviderKey;

    /// <summary>Paths served straight from the content item index, so filters and
    /// sorts on them become part of the query rather than post-processing.</summary>
    public static class OptionRowPaths
    {
        public const string DisplayText = "DisplayText";
        public const string ContentType = "ContentType";
        public const string Owner = "Owner";
        public const string Author = "Author";
        public const string Published = "Published";
        public const string CreatedUtc = "CreatedUtc";
        public const string ModifiedUtc = "ModifiedUtc";

        /// <summary>Prefix for values read from the item's own document, e.g.
        /// "Field:CustomerPart.Status". Matched in memory after the query.</summary>
        public const string FieldPrefix = "Field:";

        public static bool IsIndexed(string path) => path is
            DisplayText or ContentType or Owner or Author or Published or CreatedUtc or ModifiedUtc;
    }

    public Task<IReadOnlyList<OptionSourceColumnDescriptor>> DescribeColumnsAsync(string qualifier, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<OptionSourceColumnDescriptor>>(
        [
            new(OptionRowPaths.DisplayText, "Name", IsDefaultDisplay: true),
            new(OptionRowPaths.Owner, "Owner"),
            new(OptionRowPaths.Published, "Published"),
            new(OptionRowPaths.CreatedUtc, "Created"),
            new(OptionRowPaths.ModifiedUtc, "Modified"),
        ]);

    public async Task<IReadOnlyList<OptionRow>> QueryAsync(OptionSourceQuery query, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query.Qualifier))
        {
            return [];
        }

        // Authorization BEFORE configuration: what this user may see bounds what the
        // dropdown may offer, never the other way round.
        var scope = await scopeResolver.ResolveAsync(query.Qualifier, cancellationToken);
        if (scope.IsEmpty)
        {
            // Fail closed. Skipping the restriction here would hand every record to a
            // user entitled to none.
            return [];
        }

        var plan = Pushdown(query.Filters);

        var items = await BuildQuery(query.Qualifier, plan.Indexed, query.SearchText, scope)
            // Only paging is deferred when residual filters exist: taking the page
            // before matching them would drop rows that should have been on it.
            .Skip(plan.Residual.Count == 0 ? query.Skip : 0)
            .Take(plan.Residual.Count == 0 ? query.Take : query.Take * PageOversampleFactor)
            .ListAsync();

        var rows = items.Select(item => ToRow(item, query.Columns));

        if (plan.Residual.Count > 0)
        {
            rows = OptionFilterMatcher.Apply(rows, plan.Residual);
            // The candidate set is materialized on this path, so the instance sort
            // can apply before the page is taken.
            rows = OptionRowSorter.Apply(rows, query.SortColumns);
            rows = rows.Skip(query.Skip).Take(query.Take);
        }
        else if (query.SortColumns is { Count: > 0 })
        {
            // Paging already happened in SQL (ordered by DisplayText), so a
            // different sort can only be honored within the returned page. Correct
            // cross-page sorting for arbitrary columns needs index pushdown - a
            // recorded gap, not a silent one.
            rows = OptionRowSorter.Apply(rows, query.SortColumns);
        }

        return [.. rows];
    }

    public async Task<IReadOnlyList<OptionRow>> GetByIdsAsync(string qualifier, IReadOnlyList<string> ids, IReadOnlyList<string> columns, CancellationToken cancellationToken = default)
    {
        if (ids.Count == 0 || string.IsNullOrWhiteSpace(qualifier))
        {
            return [];
        }

        // Deliberately unfiltered by CURATION: a document written years ago references
        // whatever it referenced, and an inactive customer must still render on it.
        // Hidden means unselectable, not unresolvable.
        //
        // CONSTRAINED BY TYPE, though. Looking ids up without the content type would
        // let a caller resolve ids of ANY type through a source qualified as one -
        // scope is computed for the qualifier, so an unconstrained lookup would be
        // asking about one type while returning another.
        var items = await session
            .Query<ContentItem, ContentItemIndex>(index =>
                index.ContentItemId.IsIn(ids) && index.ContentType == qualifier && index.Latest)
            .ListAsync();

        // AUTHORIZATION is the exception, and this is where the bypass would otherwise
        // be: a user who cannot QUERY other people's records could enumerate them by
        // feeding ids to resolve.
        //
        // Out-of-scope ids are simply ABSENT from the result - not blanked in place.
        // A row the caller may not see must not come back as a hollow placeholder,
        // because a hollow row still asserts "this reference exists and resolves",
        // and a caller can act on that (totalling a list, counting matches) as though
        // the record were merely unnamed. Missing data is honest; fabricated data is
        // not. The referencing document is then itself in violation and its own
        // scope check drops it - see OptionSourceReferenceGuard.
        var scope = await scopeResolver.ResolveAsync(qualifier, cancellationToken);

        return
        [
            .. items
                .Where(item => scope.Allows(item.ContentType, item.Owner, item.ContentItemId))
                .Select(item => ToRow(item, columns)),
        ];
    }

    // Residual filters are matched after the query, so the page has to be drawn from a
    // wider slice than the caller asked for. This bounds that widening; a source whose
    // residual filters reject more than this per page wants an index, not a bigger
    // multiplier.
    private const int PageOversampleFactor = 4;

    private IQuery<ContentItem, ContentItemIndex> BuildQuery(
        string contentType,
        IReadOnlyList<ResolvedOptionFilter> indexed,
        string? searchText,
        OptionSourceScope scope)
    {
        var query = session.Query<ContentItem, ContentItemIndex>(index =>
            index.ContentType == contentType && index.Latest);

        // The scope predicate is AND-ed on and cannot be opted out of by any
        // attachment. Owner-only access narrows to the user's own items, mirroring
        // DefaultContentsAdminListFilterProvider's own/any bucketing.
        if (!scope.IsUnrestricted && scope.ViewAny.Count == 0)
        {
            var userId = scope.UserId;
            query = query.Where(index => index.Owner == userId);
        }

        // Assignment narrows further, and pushes into SQL rather than filtering after
        // the fact - the whole reason assignments carry their own index. (An empty
        // AssignedIds set never reaches here: scope.IsEmpty short-circuits first.)
        if (scope.AssignedIds is { Count: > 0 } assignedIds)
        {
            query = query.Where(index => index.ContentItemId.IsIn(assignedIds));
        }

        foreach (var filter in indexed)
        {
            var value = filter.Values.FirstOrDefault();

            // Each arm narrows the SQL rather than the materialized list - this is the
            // whole point of the provider.
            query = filter.Path switch
            {
                OptionRowPaths.DisplayText when filter.Operator == OptionFilterOperators.Contains =>
                    query.Where(index => index.DisplayText.Contains(value)),
                OptionRowPaths.DisplayText =>
                    query.Where(index => index.DisplayText == value),
                OptionRowPaths.Owner => query.Where(index => index.Owner == value),
                OptionRowPaths.Author => query.Where(index => index.Author == value),
                OptionRowPaths.Published => query.Where(index => index.Published == IsTrue(value)),
                OptionRowPaths.CreatedUtc when filter.Operator == OptionFilterOperators.GreaterThan =>
                    query.Where(index => index.CreatedUtc > AsDate(value)),
                OptionRowPaths.CreatedUtc when filter.Operator == OptionFilterOperators.LessThan =>
                    query.Where(index => index.CreatedUtc < AsDate(value)),
                OptionRowPaths.ModifiedUtc when filter.Operator == OptionFilterOperators.GreaterThan =>
                    query.Where(index => index.ModifiedUtc > AsDate(value)),
                OptionRowPaths.ModifiedUtc when filter.Operator == OptionFilterOperators.LessThan =>
                    query.Where(index => index.ModifiedUtc < AsDate(value)),
                _ => query,
            };
        }

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            var needle = searchText.Trim();
            query = query.Where(index => index.DisplayText.Contains(needle));
        }

        return query.OrderBy(index => index.DisplayText);
    }

    /// <summary>
    /// Splits filters into those the index can answer and those that need the item's
    /// document. Pure and internal so the split is unit-testable: whether a filter
    /// reaches the database is a performance contract, not an implementation detail.
    /// </summary>
    internal static FilterPushdownPlan Pushdown(IReadOnlyList<ResolvedOptionFilter>? filters)
    {
        if (filters is null || filters.Count == 0)
        {
            return new FilterPushdownPlan([], []);
        }

        var indexed = new List<ResolvedOptionFilter>();
        var residual = new List<ResolvedOptionFilter>();

        foreach (var filter in filters)
        {
            // Multi-value comparisons and negation do not map onto the single-column
            // predicates above, so they stay in memory rather than being translated
            // into something subtly different.
            var translatable =
                OptionRowPaths.IsIndexed(filter.Path)
                && filter.Values.Count == 1
                && filter.Operator is not (OptionFilterOperators.NotEquals or OptionFilterOperators.In);

            (translatable ? indexed : residual).Add(filter);
        }

        return new FilterPushdownPlan(indexed, residual);
    }

    private static bool IsTrue(string? value) =>
        string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) || value == "1";

    private static DateTime AsDate(string? value) =>
        DateTime.TryParse(value, out var parsed) ? parsed.ToUniversalTime() : DateTime.MinValue;

    // Internal for tests: the projection is what a picker actually displays.
    internal static OptionRow ToRow(ContentItem item, IReadOnlyList<string> columns)
    {
        var requested = columns is { Count: > 0 } ? columns : [OptionRowPaths.DisplayText];
        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        // ContentItem.Content is a dynamic view (JsonDynamicObject) over the item's
        // JSON. Convert once, through its implicit operator, rather than letting every
        // field read go through dynamic dispatch.
        JsonObject? document = item.Content is JsonDynamicObject dynamicObject
            ? (JsonObject)dynamicObject
            : item.Content as JsonObject;

        foreach (var column in requested)
        {
            values[column] = column switch
            {
                OptionRowPaths.DisplayText => item.DisplayText,
                OptionRowPaths.ContentType => item.ContentType,
                OptionRowPaths.Owner => item.Owner,
                OptionRowPaths.Author => item.Author,
                OptionRowPaths.Published => item.Published ? "true" : "false",
                OptionRowPaths.CreatedUtc => item.CreatedUtc?.ToString("o"),
                OptionRowPaths.ModifiedUtc => item.ModifiedUtc?.ToString("o"),
                _ when column.StartsWith(OptionRowPaths.FieldPrefix, StringComparison.OrdinalIgnoreCase) =>
                    ReadField(document, column[OptionRowPaths.FieldPrefix.Length..]),
                _ => ReadField(document, column),
            };
        }

        // A content item's identity IS its id, exactly as with users, so Key stays
        // null and the index records a null SelectedKey for this source.
        return new OptionRow(item.ContentItemId, values);
    }

    /// <summary>
    /// Walks a dotted path through the item's own document, unwrapping the common
    /// field shapes so "CustomerPart.Status" works whether Status is a text field, an
    /// option picker or a plain value.
    /// </summary>
    internal static string? ReadField(JsonNode? content, string path)
    {
        JsonNode? node = content;
        foreach (var segment in path.Split('.', StringSplitOptions.RemoveEmptyEntries))
        {
            if (node is null)
            {
                return null;
            }

            node = node[segment];
        }

        if (node is null)
        {
            return null;
        }

        // An option picker holds SelectedIds - comparing against the first is what
        // makes "customers whose Type is the type chosen above" work.
        if (node["SelectedIds"] is JsonArray { Count: > 0 } selected)
        {
            return selected[0]?.ToString();
        }

        return (node["Text"] ?? node["Value"] ?? node)?.ToString();
    }
}

/// <summary>Which filters reached the database and which did not.</summary>
internal sealed record FilterPushdownPlan(
    IReadOnlyList<ResolvedOptionFilter> Indexed,
    IReadOnlyList<ResolvedOptionFilter> Residual);
