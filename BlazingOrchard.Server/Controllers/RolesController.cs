using Microsoft.AspNetCore.Mvc;
using OrchardCore.Security.Services;

namespace BlazingOrchard.Controllers;

[ApiController]
[IgnoreAntiforgeryToken]
[Route("api/blazing/roles")]
public sealed class RolesController(IRoleService roleService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<Role[]>> List()
    {
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
