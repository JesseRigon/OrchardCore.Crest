using OrchardCore.ContentManagement;

namespace Crest.Models;

/// <summary>
/// Who or what a content item is assigned to. An item may carry SEVERAL assignments
/// at once, each naming a different kind of target.
/// </summary>
/// <remarks>
/// <para>
/// Assignment is deliberately NOT Orchard's <c>Owner</c>. <c>Owner</c> records who
/// CREATED a record and is what the stock own-content permission variations key on;
/// reassigning work by rewriting it would destroy the audit answer to "who entered
/// this" and silently change the meaning of every `ViewOwnContent`-style grant. They
/// answer different questions, so they are different fields.
/// </para>
/// <para>
/// MULTI-TARGET by design: a GPS tracker can be assigned to an asset AND a user AND
/// an organization simultaneously, and every one of those is a real assignment rather
/// than one "primary" plus extras. Each entry names its own <see cref="CrestAssignment.Kind"/>,
/// which is what lets a query ask for any one of them or all of them together.
/// </para>
/// </remarks>
public class CrestAssignmentPart : ContentPart
{
    public List<CrestAssignment> Assignments { get; set; } = [];
}

/// <summary>One assignment: a target, and the role that target plays.</summary>
public sealed class CrestAssignment
{
    /// <summary>
    /// The role this assignment represents - "SalesRep", "Technician", "Owner
    /// organization". Assignments are matched BY KIND, so this is the discriminator
    /// that keeps several assignments on one item distinguishable.
    /// </summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>
    /// What the target IS - a content type name, or
    /// <see cref="CrestAssignmentTargets.User"/> for Orchard users, who are not
    /// content items.
    /// </summary>
    public string TargetType { get; set; } = string.Empty;

    /// <summary>
    /// The target's id: a <c>ContentItemId</c> for content targets, a <c>UserId</c>
    /// for user targets.
    /// </summary>
    public string TargetId { get; set; } = string.Empty;
}

public static class CrestAssignmentTargets
{
    /// <summary>
    /// Orchard users are not content items, so they cannot be addressed by content
    /// type. This sentinel marks a user-targeted assignment.
    /// </summary>
    public const string User = "@user";

    public static bool IsUser(string? targetType) =>
        string.Equals(targetType, User, StringComparison.OrdinalIgnoreCase);
}
