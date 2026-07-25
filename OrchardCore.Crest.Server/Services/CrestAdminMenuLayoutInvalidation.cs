using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using OrchardCore.AdminMenu;
using OrchardCore.Environment.Shell;

namespace Crest.Services;

public sealed class CrestAdminMenuLayoutHub(
    IAuthorizationService authorization,
    ShellSettings shellSettings) : Hub
{
    public const string EventName = "adminMenuLayoutInvalidated";

    public override async Task OnConnectedAsync()
    {
        if (Context.User?.Identity?.IsAuthenticated != true ||
            !await authorization.AuthorizeAsync(Context.User, AdminMenuPermissions.ManageAdminMenu))
        {
            Context.Abort();
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(shellSettings.Name));
        await base.OnConnectedAsync();
    }

    internal static string GroupName(string tenantName) => "crest-admin-menu-layout:" + tenantName;
}

public interface ICrestAdminMenuLayoutInvalidator
{
    Task InvalidateTenantAsync(CancellationToken cancellationToken = default);
}

public sealed class CrestAdminMenuLayoutInvalidator(
    IHubContext<CrestAdminMenuLayoutHub> hub,
    ShellSettings shellSettings) : ICrestAdminMenuLayoutInvalidator
{
    public Task InvalidateTenantAsync(CancellationToken cancellationToken = default) =>
        hub.Clients.Group(CrestAdminMenuLayoutHub.GroupName(shellSettings.Name))
            .SendAsync(CrestAdminMenuLayoutHub.EventName, cancellationToken);
}
