using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using OrchardCore.Admin;
using OrchardCore.Environment.Shell;
using OrchardCore.Security;

namespace Crest.Services;

public sealed class CrestPermissionHub(
    IAuthorizationService authorization,
    ShellSettings shellSettings) : Hub
{
    public const string EventName = "permissionsInvalidated";

    public override async Task OnConnectedAsync()
    {
        if (Context.User?.Identity?.IsAuthenticated != true ||
            !await authorization.AuthorizeAsync(Context.User, AdminPermissions.AccessAdminPanel))
        {
            Context.Abort();
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(shellSettings.Name));
        await base.OnConnectedAsync();
    }

    internal static string GroupName(string tenantName) => "crest-permissions:" + tenantName;
}

/// <summary>
/// Broadcasts an invalidation only. Browsers refetch the normal, authorized
/// manifest instead of receiving permissions through the hub.
/// </summary>
public interface ICrestPermissionInvalidator
{
    Task InvalidateTenantAsync(CancellationToken cancellationToken = default);
}

public sealed class CrestPermissionInvalidator(
    IHubContext<CrestPermissionHub> hub,
    ShellSettings shellSettings) : ICrestPermissionInvalidator
{
    public Task InvalidateTenantAsync(CancellationToken cancellationToken = default) =>
        hub.Clients.Group(CrestPermissionHub.GroupName(shellSettings.Name))
            .SendAsync(CrestPermissionHub.EventName, cancellationToken);
}

/// <summary>Receives native Orchard role permission updates, including changes made outside Crest.</summary>
public sealed class CrestRolePermissionInvalidationHandler(ICrestPermissionInvalidator invalidator) : IRoleUpdatedEventHandler
{
    public Task RoleUpdatedAsync(string roleName) => invalidator.InvalidateTenantAsync();
}
