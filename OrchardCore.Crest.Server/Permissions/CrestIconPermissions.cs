using OrchardCore.Security.Permissions;

namespace Crest.Security;

public sealed class CrestIconPermissions : IPermissionProvider
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
