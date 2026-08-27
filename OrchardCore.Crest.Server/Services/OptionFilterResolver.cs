using System.Text.Json.Nodes;
using Crest.Settings;

namespace Crest.Services;

/// <summary>
/// Turns configured <see cref="OptionFilter"/>s into <see cref="ResolvedOptionFilter"/>s
/// by reading <see cref="OptionFilter.ValueFrom"/> values out of the item being edited.
/// Providers never see an unresolved filter - by the time a query reaches one, every
/// comparison value is a literal.
/// </summary>
/// <remarks>
/// Pure and static on purpose: dependent-dropdown semantics are the part most likely
/// to be got subtly wrong (an empty parent silently matching everything, a filter
/// quietly dropped), so they are testable without a session, a provider or a browser.
/// </remarks>
public static class OptionFilterResolver
{
    /// <summary>
    /// Resolves every filter against the editing item's state.
    /// </summary>
    /// <param name="filters">The attachment's configured filters.</param>
    /// <param name="editorState">
    /// Current values of the item being edited, keyed by the same paths
    /// <see cref="OptionFilter.ValueFrom"/> uses. A field holding several values (a
    /// multi-select parent) contributes all of them.
    /// </param>
    public static OptionFilterResolution Resolve(
        IEnumerable<OptionFilter>? filters,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? editorState)
    {
        if (filters is null)
        {
            return OptionFilterResolution.Unrestricted;
        }

        var resolved = new List<ResolvedOptionFilter>();
        var unmet = new List<string>();

        foreach (var filter in filters)
        {
            if (string.IsNullOrWhiteSpace(filter.Path))
            {
                continue;
            }

            if (!filter.IsDependent)
            {
                // A static filter with no value is meaningless rather than
                // "matches empty" - skip it instead of silently excluding every row.
                if (filter.Value is not null)
                {
                    resolved.Add(ResolvedOptionFilter.From(filter, SplitLiteral(filter.Operator, filter.Value)));
                }

                continue;
            }

            var values = ReadFrom(editorState, filter.ValueFrom!);

            if (values.Count == 0)
            {
                // The parent has not been chosen yet. Either the child offers nothing
                // (the filter is unmet), or the restriction simply does not apply yet.
                // It must never resolve to "compare against empty", which would match
                // only rows whose value is blank.
                if (filter.RequireParentValue)
                {
                    unmet.Add(filter.ValueFrom!);
                }

                continue;
            }

            resolved.Add(ResolvedOptionFilter.From(filter, values));
        }

        return new OptionFilterResolution(resolved, unmet);
    }

    /// <summary>
    /// True when <paramref name="selectedIds"/> is still valid under
    /// <paramref name="resolution"/> - the check behind clearing a child whose parent
    /// just changed. An empty selection is always valid; there is nothing to
    /// contradict.
    /// </summary>
    public static bool SelectionStillValid(
        OptionFilterResolution resolution,
        IReadOnlyList<string> selectedIds,
        IReadOnlyList<OptionRow> selectedRows)
    {
        if (selectedIds.Count == 0)
        {
            return true;
        }

        // A filter that cannot be satisfied at all invalidates whatever is selected:
        // the parent was cleared, so the child cannot still belong to it.
        if (resolution.HasUnmetDependencies)
        {
            return false;
        }

        foreach (var id in selectedIds)
        {
            var row = selectedRows.FirstOrDefault(candidate =>
                string.Equals(candidate.Id, id, StringComparison.OrdinalIgnoreCase));

            // A selected id the source can no longer produce is stale by definition.
            if (row is null || !resolution.Filters.All(filter => OptionFilterMatcher.Matches(row, filter)))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// The paths this attachment depends on, so an editor knows which fields to watch.
    /// Watching is what triggers a re-query - on the parent's value COMMITTING, not on
    /// every keystroke, since the user cannot be editing both fields at once and a
    /// per-keypress dependent query is wasted compute.
    /// </summary>
    public static IReadOnlyList<string> DependencyPaths(IEnumerable<OptionFilter>? filters) =>
        filters is null
            ? []
            : [.. filters
                .Where(filter => filter.IsDependent)
                .Select(filter => filter.ValueFrom!)
                .Distinct(StringComparer.OrdinalIgnoreCase)];

    /// <summary>
    /// Whether a dependent field must be filled in to complete the document. DERIVED
    /// from the parent rather than configured separately: if the parent is required,
    /// leaving the child unresolved would save an incomplete pair, so the child is
    /// required too.
    /// </summary>
    public static bool IsRequiredBecauseParentIs(
        IEnumerable<OptionFilter>? filters,
        Func<string, bool> isPathRequired) =>
        DependencyPaths(filters).Any(isPathRequired);

    /// <summary>
    /// Reads a field's current value(s) from editor state. Values are looked up by the
    /// configured path directly - the editor supplies state keyed the same way the
    /// settings name it, so no path dialect is invented here.
    /// </summary>
    private static IReadOnlyList<string> ReadFrom(
        IReadOnlyDictionary<string, IReadOnlyList<string>>? editorState,
        string path)
    {
        if (editorState is null || !editorState.TryGetValue(path, out var values) || values is null)
        {
            return [];
        }

        return [.. values.Where(value => !string.IsNullOrWhiteSpace(value))];
    }

    /// <summary>
    /// "In" takes a list; every other operator takes the literal as written, so a
    /// value that legitimately contains a comma is not split behind the user's back.
    /// </summary>
    private static IReadOnlyList<string> SplitLiteral(string @operator, string value) =>
        string.Equals(@operator, OptionFilterOperators.In, StringComparison.OrdinalIgnoreCase)
            ? [.. value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)]
            : [value];

    /// <summary>
    /// Builds editor state from a content item's JSON, for callers that have the item
    /// rather than a live editor. Paths are "PartName.FieldName"; an OptionPickerField
    /// contributes its selected ids, and simpler fields their text value.
    /// </summary>
    public static IReadOnlyDictionary<string, IReadOnlyList<string>> StateFromContent(JsonNode? content, IEnumerable<string> paths)
    {
        var state = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        if (content is null)
        {
            return state;
        }

        foreach (var path in paths)
        {
            var node = content;
            foreach (var segment in path.Split('.', StringSplitOptions.RemoveEmptyEntries))
            {
                node = node?[segment];
            }

            if (node is null)
            {
                continue;
            }

            // An option picker stores SelectedIds; a text/numeric field stores a
            // scalar. Read whichever this field turns out to be.
            var selected = node["SelectedIds"];
            if (selected is JsonArray array)
            {
                state[path] = [.. array.Select(entry => entry?.ToString()).Where(entry => !string.IsNullOrWhiteSpace(entry))!];
                continue;
            }

            var scalar = node["Text"] ?? node["Value"] ?? (node is JsonValue ? node : null);
            if (scalar is not null)
            {
                state[path] = [scalar.ToString()];
            }
        }

        return state;
    }
}

/// <summary>
/// The outcome of resolving an attachment's filters: what to apply, and whether a
/// required parent is still missing.
/// </summary>
public sealed record OptionFilterResolution(
    IReadOnlyList<ResolvedOptionFilter> Filters,
    IReadOnlyList<string> UnmetDependencies)
{
    public static readonly OptionFilterResolution Unrestricted = new([], []);

    /// <summary>
    /// True when a filter needs a parent value that has not been supplied. The query
    /// must then return NOTHING rather than everything - the whole point of requiring
    /// the parent is that an unfiltered list is the wrong answer.
    /// </summary>
    public bool HasUnmetDependencies => UnmetDependencies.Count > 0;
}
