using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrchardCore.Roles;
using OrchardCore.Security.Services;

namespace Crest.Controllers;

[ApiController]
[AutoValidateAntiforgeryToken]
[Route("api/crest/roles")]
public sealed class RolesController(IRoleService roleService, IAuthorizationService authorization) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<Role[]>> List()
    {
        if (!await authorization.AuthorizeAsync(User, RolesPermissions.ManageRoles)) return Forbid();
        var roles = await roleService.GetRolesAsync();
        var result = new List<Role>();

        foreach (var role in roles)
        {
            result.Add(new Role(
                role.RoleName,
                role.RoleDescription,
                await roleService.IsAdminRoleAsync(role.RoleName),
                await roleService.IsSystemRoleAsync(role.RoleName)));
        }

        return Ok(result.OrderBy(role => role.Name).ToArray());
    }
}

public sealed record Role(string Name, string Description, bool IsAdmin, bool IsSystem);
