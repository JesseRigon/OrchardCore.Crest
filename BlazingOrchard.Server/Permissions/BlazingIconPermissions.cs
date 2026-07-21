using OrchardCore.Security.Permissions;

namespace BlazingOrchard.Security;

public sealed class BlazingIconPermissions : IPermissionProvider
{
    public static readonly Permission ManageTenantIcons = new(nameof(ManageTenantIcons), "Manage tenant icons", isSecurityCritical: true);

    public Task<IEnumerable<Permission>> GetPermissionsAsync() => Task.FromResult<IEnumerable<Permission>>([ManageTenantIcons]);

    public IEnumerable<PermissionStereotype> GetDefaultStereotypes() =>
    [
        new()
        {
            Name = "Administrator",
            Permissions = [ManageTenantIcons]
        }
    ];
}
