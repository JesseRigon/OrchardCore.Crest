namespace Crest.Services;

/// <summary>
/// What the CURRENT USER may see from a content-item source, independent of what the
/// dropdown is configured to offer.
/// </summary>
/// <remarks>
/// <para>
/// Filters and scope are different things and must not be conflated. A FILTER says
/// what this dropdown displays and is per-attachment configuration; SCOPE says what
/// this user is permitted to see and is not configurable at all. Scope cannot be
/// expressed as a filter for two reasons: its value is not known when the attachment
/// is configured (it depends on who is asking), and anything expressible in
/// per-attachment settings is omittable by configuring another attachment without it.
/// Authorization that any configuration can omit is not authorization.
/// </para>
/// <para>
/// The MECHANISM here is Orchard's, not ours: <c>AuthorizeContentTypeAsync</c> with
/// <c>owner: null</c> asks "may they view OTHERS' items of this type?" and with the
/// user's id asks "may they view their OWN?". That is the same pair of questions
/// <c>DefaultContentsAdminListFilterProvider</c> asks when it scopes the admin content
/// list, and this type deliberately mirrors its bucketing so the two behave alike.
/// </para>
/// </remarks>
public sealed record OptionSourceScope(
    /// <summary>Types the user may see in full.</summary>
    IReadOnlyList<string> ViewAny,
    /// <summary>Types the user may see only their own items of.</summary>
    IReadOnlyList<string> ViewOwn,
    /// <summary>The current user's id, for matching <c>ContentItemIndex.Owner</c>.</summary>
    string? UserId)
{
    /// <summary>
    /// Ids the user reaches by ASSIGNMENT rather than ownership. Null means no
    /// assignment constraint applies; an EMPTY set means the constraint applies and
    /// nothing matched, which denies everything. The two must never be conflated -
    /// treating "nothing matched" as "unconstrained" would invert the restriction.
    /// </summary>
    public IReadOnlyCollection<string>? AssignedIds { get; init; }

    /// <summary>
    /// Nothing is viewable. Callers MUST return an empty result rather than skipping
    /// the restriction - this is the fail-closed case, and treating it as "no filter"
    /// would return everything to a user entitled to nothing.
    /// </summary>
    public static readonly OptionSourceScope Nothing = new([], [], null);

    /// <summary>
    /// Everything is viewable, so no predicate is needed. Used for sources that carry
    /// no per-item authorization (option lists are tenant configuration, not records).
    /// </summary>
    public static readonly OptionSourceScope Unrestricted = new([], [], null) { IsUnrestricted = true };

    public bool IsUnrestricted { get; private init; }

    /// <summary>True when the user may see nothing at all.</summary>
    public bool IsEmpty =>
        !IsUnrestricted
        && ((ViewAny.Count == 0 && ViewOwn.Count == 0)
            // An assignment constraint that matched nothing denies everything, even
            // when the permission check itself passed.
            || AssignedIds is { Count: 0 });

    /// <summary>
    /// Whether a specific item is in scope. Used by resolve, which cannot express
    /// itself as a query predicate because it looks items up by id.
    /// </summary>
    public bool Allows(string contentType, string? owner, string? contentItemId = null)
    {
        if (IsUnrestricted)
        {
            return true;
        }

        // Assignment narrows whatever the permission check granted - it never widens
        // it, so it is applied on top rather than as an alternative.
        if (AssignedIds is not null
            && (contentItemId is null || !AssignedIds.Contains(contentItemId, StringComparer.OrdinalIgnoreCase)))
        {
            return false;
        }

        if (ViewAny.Contains(contentType, StringComparer.OrdinalIgnoreCase))
        {
            return true;
        }

        return ViewOwn.Contains(contentType, StringComparer.OrdinalIgnoreCase)
            && UserId is not null
            && string.Equals(owner, UserId, StringComparison.Ordinal);
    }
}
