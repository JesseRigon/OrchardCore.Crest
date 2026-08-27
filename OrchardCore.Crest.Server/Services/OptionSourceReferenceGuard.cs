namespace Crest.Services;

/// <summary>
/// Decides whether an item survives its own references: if an item points at a record
/// the caller may not see, the item is out of scope too.
/// </summary>
/// <remarks>
/// <para>
/// The rule is "unresolvable reference ⇒ the referencing item is out of scope", not
/// "blank the reference". An invoice whose party the caller cannot see is itself in
/// violation: returning it with an empty party produces a record that LOOKS complete
/// and is not, and a caller can total or count such records as though the missing
/// piece were merely unnamed. It generalizes past parties - a manager who cannot see
/// an assignee must not see that assignee's tasks either.
/// </para>
/// <para>
/// This makes scope TRANSITIVE, which is why the traversal below is bounded. Two
/// hazards come with transitivity and both are guarded: CYCLES (A references B, B
/// references A) via a visited set, and DEPTH (a chain long enough to be a denial of
/// service) via <see cref="MaxDepth"/>. Exceeding the depth limit DENIES rather than
/// allows - an answer that cannot be computed within budget is not evidence of
/// permission.
/// </para>
/// </remarks>
public static class OptionSourceReferenceGuard
{
    /// <summary>
    /// How far a reference chain is followed before the item is denied. Chains this
    /// deep in an ERP document graph indicate a modelling problem rather than a real
    /// access question.
    /// </summary>
    public const int MaxDepth = 8;

    /// <summary>
    /// Whether <paramref name="rootId"/> survives, given a way to read an item's
    /// references and a way to test whether one is visible.
    /// </summary>
    /// <param name="rootId">The item being tested.</param>
    /// <param name="getReferences">
    /// The ids this item points at. Only references that PARTICIPATE IN SCOPE belong
    /// here - a reference to tenant configuration (an option list) is not a scope
    /// question and must not be included, or every document would depend on
    /// configuration visibility.
    /// </param>
    /// <param name="isVisible">Whether a referenced id is itself visible.</param>
    public static bool Survives(
        string rootId,
        Func<string, IReadOnlyList<string>> getReferences,
        Func<string, bool> isVisible)
    {
        // Visited carries across the whole traversal, not per branch: revisiting an id
        // down a different path would recompute the same answer, and in a cycle would
        // never terminate.
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        return Survives(rootId, getReferences, isVisible, visited, depth: 0);
    }

    private static bool Survives(
        string id,
        Func<string, IReadOnlyList<string>> getReferences,
        Func<string, bool> isVisible,
        HashSet<string> visited,
        int depth)
    {
        if (depth > MaxDepth)
        {
            // Fail closed. A budget overrun is an unanswered question, and an
            // unanswered authorization question is a denial.
            return false;
        }

        // Already proven to survive on another path - a cycle returns here rather than
        // recursing forever.
        if (!visited.Add(id))
        {
            return true;
        }

        foreach (var reference in getReferences(id))
        {
            if (string.IsNullOrWhiteSpace(reference))
            {
                continue;
            }

            if (!isVisible(reference))
            {
                return false;
            }

            if (!Survives(reference, getReferences, isVisible, visited, depth + 1))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Filters a set of items to those surviving their own references. The batch form
    /// exists so a list is resolved with shared traversal state rather than one
    /// independent walk per row.
    /// </summary>
    public static IReadOnlyList<T> Filter<T>(
        IEnumerable<T> items,
        Func<T, string> getId,
        Func<string, IReadOnlyList<string>> getReferences,
        Func<string, bool> isVisible) =>
        [.. items.Where(item => Survives(getId(item), getReferences, isVisible))];
}
