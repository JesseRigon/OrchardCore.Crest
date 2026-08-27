using Crest.Models;
using OrchardCore.ContentManagement;
using YesSql.Indexes;

namespace Crest.Indexing;

/// <summary>
/// One row per ASSIGNMENT, not per item: an item with three assignments emits three
/// rows.
/// </summary>
/// <remarks>
/// <para>
/// This shape is what makes "either or all" a QUERY rather than post-processing.
/// Matching any of a set of assignments is one predicate over these rows; matching
/// ALL of them is a grouped count that equals the number required. Neither needs the
/// items materialized first, which is the whole point - scoping a customer table by
/// assignment must not mean loading every customer and discarding most of them.
/// </para>
/// <para>
/// Tenant scoping is inherent: <c>TablePrefix</c> is a per-tenant shell setting
/// applied at YesSql store configuration, so there is no tenant discriminator column
/// and no cross-tenant query path.
/// </para>
/// </remarks>
public class CrestAssignmentIndex : MapIndex
{
    /// <summary>The item that carries the assignment.</summary>
    public string ContentItemId { get; set; }

    /// <summary>The assigned item's own type, so a scope predicate can narrow to one
    /// type without joining back to <c>ContentItemIndex</c>.</summary>
    public string ContentType { get; set; }

    /// <summary>The role this assignment represents ("SalesRep", "Technician").</summary>
    public string Kind { get; set; }

    /// <summary>What the target is: a content type name, or "@user".</summary>
    public string TargetType { get; set; }

    /// <summary>The target's ContentItemId or UserId.</summary>
    public string TargetId { get; set; }

    /// <summary>Only the latest version is scoped; historical versions are not
    /// separately assignable.</summary>
    public bool Latest { get; set; }
}

/// <summary>
/// Maps items carrying <see cref="CrestAssignmentPart"/> into one row per assignment.
/// </summary>
public sealed class CrestAssignmentIndexProvider : IndexProvider<ContentItem>
{
    public override void Describe(DescribeContext<ContentItem> context)
    {
        context.For<CrestAssignmentIndex>()
            .Map(contentItem =>
            {
                // Only the LATEST version participates in scoping.
                //
                // Stock field indexes use `if (!Published && !Latest) skip`, keeping
                // published-but-superseded rows and recording Latest as a column to
                // filter on. That is right for display indexes, where an older
                // published version is still a legitimate thing to render. It is
                // wrong here: this index answers an AUTHORIZATION question, and a
                // superseded assignment must stop granting access the moment it is
                // replaced. Keeping those rows would mean a reassigned record still
                // resolved for its previous assignee through the old version.
                if (!contentItem.Latest)
                {
                    return Enumerable.Empty<CrestAssignmentIndex>();
                }

                var part = contentItem.As<CrestAssignmentPart>();
                if (part?.Assignments is not { Count: > 0 })
                {
                    return Enumerable.Empty<CrestAssignmentIndex>();
                }

                return part.Assignments
                    .Where(assignment =>
                        !string.IsNullOrWhiteSpace(assignment.Kind)
                        && !string.IsNullOrWhiteSpace(assignment.TargetId))
                    .Select(assignment => new CrestAssignmentIndex
                    {
                        ContentItemId = contentItem.ContentItemId,
                        ContentType = contentItem.ContentType,
                        Kind = assignment.Kind,
                        TargetType = assignment.TargetType,
                        TargetId = assignment.TargetId,
                        Latest = true,
                    });
            });
    }
}
