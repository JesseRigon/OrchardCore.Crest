using BlazingOrchard.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BlazingOrchard.Controllers;

[ApiController]
[IgnoreAntiforgeryToken]
[Route("api/blazing/icons")]
public sealed class IconsController(
    IAuthorizationService authorizationService,
    BlazingIconSourceStore iconSourceStore) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<BlazingIconSearchResult>> SearchAsync(
        [FromQuery] string? library,
        [FromQuery] string? query,
        [FromQuery] string[]? filter,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 200)
    {
        if (!await authorizationService.AuthorizeAsync(User, OrchardCore.AdminMenu.AdminMenuPermissions.ManageAdminMenu))
        {
            return Forbid();
        }

        return Ok(await iconSourceStore.SearchAsync(library, query, skip, take, ParseFilters(filter), HttpContext.RequestAborted));
    }

    private static BlazingIconSearchFilter[] ParseFilters(string[]? filters) =>
        (filters ?? [])
            .Select(value => value.Split(':', 2, StringSplitOptions.TrimEntries))
            .Where(parts => parts.Length == 2 && !string.IsNullOrWhiteSpace(parts[0]) && !string.IsNullOrWhiteSpace(parts[1]))
            .Select(parts => new BlazingIconSearchFilter(parts[0], parts[1]))
            .ToArray();
}
