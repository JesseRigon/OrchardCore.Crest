using OrchardCore.Security.Permissions;

namespace OrchardCore.Crest.Workflows;

public class Permissions : IPermissionProvider
{
    public static readonly Permission ManageWorkflows = new("ManageWorkflows", "Manage workflows", isSecurityCritical: true);

    public Task<IEnumerable<Permission>> GetPermissionsAsync()
    {
        return Task.FromResult(new[] { ManageWorkflows }.AsEnumerable());
    }

    public IEnumerable<PermissionStereotype> GetDefaultStereotypes()
    {
        return
        [
            new()
            {
                Name = "Administrator",
                Permissions = [ManageWorkflows]
            }
        ];
    }
}