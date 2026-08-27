using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using OrchardCore.Contents;

namespace Crest.Services;

/// <summary>
/// Builds the <see cref="OptionSourceScope"/> for the current user, using Orchard's own
/// resource-based authorization rather than a parallel permission scheme.
/// </summary>
public interface IOptionSourceScopeResolver
{
    Task<OptionSourceScope> ResolveAsync(string contentType, CancellationToken cancellationToken = default);
}

/// <summary>
/// Declares that a content type is scoped by ASSIGNMENT: users see only the items
/// assigned to them, on top of whatever the permission check already allowed.
/// </summary>
/// <remarks>
/// Registered per content type rather than inferred, because "this type is
/// assignment-scoped" is a policy decision. A type nobody declares keeps plain
/// ownership semantics.
/// </remarks>
public sealed class AssignmentScopedTypes
{
    private readonly Dictionary<string, string?> _kindsByType = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Scope <paramref name="contentType"/> to items assigned to the current user.
    /// <paramref name="kind"/> narrows to one assignment role; null accepts any.
    /// </summary>
    public AssignmentScopedTypes Add(string contentType, string? kind = null)
    {
        _kindsByType[contentType] = kind;
        return this;
    }

    public bool IsScoped(string contentType) => _kindsByType.ContainsKey(contentType);

    public string? KindFor(string contentType) =>
        _kindsByType.TryGetValue(contentType, out var kind) ? kind : null;
}

/// <inheritdoc />
public sealed class OptionSourceScopeResolver(
    IAuthorizationService authorizationService,
    IHttpContextAccessor httpContextAccessor,
    ICrestAssignmentService assignments,
    AssignmentScopedTypes assignmentScopedTypes) : IOptionSourceScopeResolver
{
    public async Task<OptionSourceScope> ResolveAsync(string contentType, CancellationToken cancellationToken = default)
    {
        var user = httpContextAccessor.HttpContext?.User;

        // No principal means no grant. Failing closed here matters: an unauthenticated
        // path that fell through to "unrestricted" would expose every record.
        if (user is null || string.IsNullOrWhiteSpace(contentType))
        {
            return OptionSourceScope.Nothing;
        }

        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);

        // The same two questions DefaultContentsAdminListFilterProvider asks. Passing
        // owner: null asks whether the user may view OTHERS' content of this type;
        // passing the user's own id asks whether they may view their OWN.
        var scope = await authorizationService.AuthorizeContentTypeAsync(user, CommonPermissions.ViewContent, contentType, owner: null)
            ? new OptionSourceScope([contentType], [], userId)
            : userId is not null
                && await authorizationService.AuthorizeContentTypeAsync(user, CommonPermissions.ViewContent, contentType, userId)
                ? new OptionSourceScope([], [contentType], userId)
                : OptionSourceScope.Nothing;

        if (scope.IsEmpty || userId is null || !assignmentScopedTypes.IsScoped(contentType))
        {
            return scope;
        }

        // Assignment NARROWS what the permission check granted; it never widens it, so
        // it is layered on rather than offered as an alternative route in.
        var assigned = await assignments.FindAssignedIdsAsync(
            contentType,
            [new AssignmentRequirement(userId, assignmentScopedTypes.KindFor(contentType))],
            AssignmentMatch.Any,
            cancellationToken);

        // A null result means "no requirements were supplied", which cannot happen
        // here - exactly one is always passed. Coalescing it to an empty set would
        // silently DENY EVERYTHING if that ever changed, so the impossible case is
        // asserted rather than papered over.
        ArgumentNullException.ThrowIfNull(assigned);

        return scope with { AssignedIds = assigned };
    }
}
