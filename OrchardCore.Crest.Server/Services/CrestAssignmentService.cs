using Crest.Indexing;
using YesSql;
using YesSql.Services;

namespace Crest.Services;

/// <summary>
/// How several assignment requirements combine.
/// </summary>
public enum AssignmentMatch
{
    /// <summary>The item matches if ANY requirement is satisfied (union).</summary>
    Any,

    /// <summary>The item matches only if EVERY requirement is satisfied
    /// (intersection).</summary>
    All,
}

/// <summary>One thing an item must be assigned to.</summary>
/// <param name="TargetId">The target's ContentItemId or UserId.</param>
/// <param name="Kind">The assignment role, or null to match any role.</param>
public sealed record AssignmentRequirement(string TargetId, string? Kind = null);

/// <summary>
/// Answers "which items are assigned to these targets" from the assignment index,
/// without materializing the items.
/// </summary>
public interface ICrestAssignmentService
{
    /// <summary>
    /// The ids of items of <paramref name="contentType"/> matching the requirements.
    /// Returns null when there are NO requirements - meaning "unconstrained", which
    /// callers must distinguish from an empty set ("constrained, nothing matched").
    /// </summary>
    Task<IReadOnlyCollection<string>?> FindAssignedIdsAsync(
        string contentType,
        IReadOnlyList<AssignmentRequirement> requirements,
        AssignmentMatch match,
        CancellationToken cancellationToken = default);

    /// <summary>What a single item is assigned to.</summary>
    Task<IReadOnlyList<CrestAssignmentIndex>> GetAssignmentsAsync(
        string contentItemId,
        CancellationToken cancellationToken = default);
}

/// <inheritdoc />
public sealed class CrestAssignmentService(ISession session) : ICrestAssignmentService
{
    public async Task<IReadOnlyCollection<string>?> FindAssignedIdsAsync(
        string contentType,
        IReadOnlyList<AssignmentRequirement> requirements,
        AssignmentMatch match,
        CancellationToken cancellationToken = default)
    {
        // No requirements means no assignment constraint at all. This MUST be
        // distinguishable from "constrained and nothing matched" - conflating them
        // would turn a user with no matching assignments into a user who sees
        // everything.
        if (requirements.Count == 0)
        {
            return null;
        }

        var targetIds = requirements.Select(requirement => requirement.TargetId).Distinct().ToArray();

        // ONE query covering every requirement, then combine over the returned rows.
        // The row set is bounded by how many items are assigned to these specific
        // targets - a rep's own book of business, not the whole customer table - so
        // combining in memory here does not reintroduce the load-everything problem
        // the index exists to avoid. What it does NOT bound is a target with an
        // enormous number of assignments; if that appears, All-matching becomes a
        // grouped HAVING COUNT(*) = n in SQL rather than this.
        var rows = await session
            .QueryIndex<CrestAssignmentIndex>(index =>
                index.ContentType == contentType
                && index.TargetId.IsIn(targetIds)
                && index.Latest)
            .ListAsync();

        var byItem = rows
            .GroupBy(row => row.ContentItemId, StringComparer.OrdinalIgnoreCase);

        var matched = match == AssignmentMatch.All
            ? byItem.Where(group => requirements.All(requirement => group.Any(row => Satisfies(row, requirement))))
            : byItem.Where(group => requirements.Any(requirement => group.Any(row => Satisfies(row, requirement))));

        return [.. matched.Select(group => group.Key)];
    }

    public async Task<IReadOnlyList<CrestAssignmentIndex>> GetAssignmentsAsync(
        string contentItemId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(contentItemId))
        {
            return [];
        }

        return [.. await session
            .QueryIndex<CrestAssignmentIndex>(index =>
                index.ContentItemId == contentItemId && index.Latest)
            .ListAsync()];
    }

    // A null Kind matches any role; a set Kind must match exactly. Internal so the
    // combination rules are unit-testable without a session.
    internal static bool Satisfies(CrestAssignmentIndex row, AssignmentRequirement requirement) =>
        string.Equals(row.TargetId, requirement.TargetId, StringComparison.OrdinalIgnoreCase)
        && (requirement.Kind is null
            || string.Equals(row.Kind, requirement.Kind, StringComparison.OrdinalIgnoreCase));
}
