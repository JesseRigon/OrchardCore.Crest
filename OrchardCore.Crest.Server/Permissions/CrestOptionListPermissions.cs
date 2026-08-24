using OrchardCore.Security.Permissions;

namespace Crest.Permissions;

public sealed class CrestOptionListPermissions : IPermissionProvider
{
    public static readonly Permission ViewOptionLists = new(nameof(ViewOptionLists), "View option lists");

    public static readonly Permission ManageOptionLists = new(nameof(ManageOptionLists), "Manage option lists", [ViewOptionLists], isSecurityCritical: true);

    public Task<IEnumerable<Permission>> GetPermissionsAsync() =>
        Task.FromResult<IEnumerable<Permission>>([ViewOptionLists, ManageOptionLists]);

    public IEnumerable<PermissionStereotype> GetDefaultStereotypes() =>
    [
        new()
        {
            Name = "Administrator",
            Permissions = [ManageOptionLists],
        },
        new()
        {
            Name = "Editor",
            Permissions = [ViewOptionLists],
        },
    ];
}
