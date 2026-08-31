using OrchardCore.Security.Permissions;

namespace Crest.Permissions;

public sealed class CrestContentPartListPermissions : IPermissionProvider
{
    public static readonly Permission ViewContentPartLists = new(nameof(ViewContentPartLists), "View content part lists");

    public static readonly Permission ManageContentPartLists = new(nameof(ManageContentPartLists), "Manage content part lists", [ViewContentPartLists], isSecurityCritical: true);

    // The tenant super admin's lever: place or lift TENANT-set locks on content part lists.
    // Module-set locks are a developer contract and stay immutable regardless of this
    // permission. Deliberately NOT implied by ManageContentPartLists - a role may curate
    // lists without being able to unfreeze what the super admin froze.
    public static readonly Permission LockContentPartLists = new(
        nameof(LockContentPartLists),
        "Lock or unlock content part lists (tenant-set locks; module-set locks stay fixed)",
        isSecurityCritical: true);

    public Task<IEnumerable<Permission>> GetPermissionsAsync() =>
        Task.FromResult<IEnumerable<Permission>>([ViewContentPartLists, ManageContentPartLists, LockContentPartLists]);

    public IEnumerable<PermissionStereotype> GetDefaultStereotypes() =>
    [
        new()
        {
            Name = "Administrator",
            Permissions = [ManageContentPartLists, LockContentPartLists],
        },
        new()
        {
            Name = "Editor",
            Permissions = [ViewContentPartLists],
        },
    ];
}
