using OrchardCore.Security.Permissions;

namespace Crest.Global;

/// <summary>
/// Editing global reference data is a super-tenant act. The permission exists only so an
/// editing surface has something to check; it is granted to no stereotype and the
/// controllers additionally require <c>ShellSettings.IsDefaultShell()</c>, so an ordinary
/// tenant has no route to it, not a hidden one (plans/global.md).
/// </summary>
public sealed class CrestGlobalPermissions : IPermissionProvider
{
    public static readonly Permission ManageGlobalReferenceData = new("ManageCrestGlobalReferenceData", "Manage global reference data (Default tenant only)");

    public Task<IEnumerable<Permission>> GetPermissionsAsync() => Task.FromResult<IEnumerable<Permission>>([ManageGlobalReferenceData]);

    public IEnumerable<PermissionStereotype> GetDefaultStereotypes() => [];
}
