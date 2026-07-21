using Crest.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Crest.Controllers;

[ApiController]
[IgnoreAntiforgeryToken]
[Route("api/crest/icons")]
public sealed class IconsController(
    IAuthorizationService authorizationService,
    CrestIconSourceStore iconSourceStore) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<CrestIconSearchResult>> SearchAsync(
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

    private static CrestIconSearchFilter[] ParseFilters(string[]? filters) =>
        (filters ?? [])
            .Select(value => value.Split(':', 2, StringSplitOptions.TrimEntries))
            .Where(parts => parts.Length == 2 && !string.IsNullOrWhiteSpace(parts[0]) && !string.IsNullOrWhiteSpace(parts[1]))
            .Select(parts => new CrestIconSearchFilter(parts[0], parts[1]))
            .ToArray();
}
