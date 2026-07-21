using Crest.Icons;
using Crest.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Crest.Controllers;

[ApiController]
[IgnoreAntiforgeryToken]
[Route("api/crest/icons/tenant")]
public sealed class TenantIconsController(
    IAuthorizationService authorizationService,
    IEnumerable<IIconProvider> iconProviders) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TenantMediaIconSummary>>> ListAsync()
    {
        var provider = GetProvider();
        if (provider is null)
        {
            return NotFound();
        }

        if (!await CanManageAsync())
        {
            return Forbid();
        }

        return Ok(await provider.ListAsync(HttpContext.RequestAborted));
    }

    [HttpPost]
    [RequestSizeLimit(TenantMediaIconProvider.MaxSvgBytes + 32 * 1024)]
    public async Task<ActionResult<TenantMediaIconSummary>> UploadAsync([FromForm] IFormFile file, [FromForm] bool overwrite = true)
    {
        var provider = GetProvider();
        if (provider is null)
        {
            return NotFound();
        }

        if (!await CanManageAsync())
        {
            return Forbid();
        }

        if (file.Length == 0 || !file.FileName.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest("A non-empty SVG file is required.");
        }

        try
        {
            await using var stream = file.OpenReadStream();
            return Ok(await provider.SaveAsync(file.FileName, stream, overwrite, HttpContext.RequestAborted));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpDelete("{name}")]
    public async Task<IActionResult> DeleteAsync(string name)
    {
        var provider = GetProvider();
        if (provider is null)
        {
            return NotFound();
        }

        if (!await CanManageAsync())
        {
            return Forbid();
        }

        return await provider.DeleteAsync(name, HttpContext.RequestAborted) ? NoContent() : NotFound();
    }

    private TenantMediaIconProvider? GetProvider() => iconProviders.OfType<TenantMediaIconProvider>().FirstOrDefault();

    private Task<bool> CanManageAsync() => authorizationService.AuthorizeAsync(User, CrestIconPermissions.ManageTenantIcons);
}
