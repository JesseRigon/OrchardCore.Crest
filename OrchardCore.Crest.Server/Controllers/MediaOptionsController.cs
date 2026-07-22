using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using OrchardCore.Media;

namespace Crest.Controllers;

[ApiController]
[AutoValidateAntiforgeryToken]
[Route("api/crest/media/options")]
public sealed class CrestMediaOptionsController(IOptions<MediaOptions> options, IAuthorizationService authorization) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<MediaOptionsDto>> GetAsync()
    {
        if (!await authorization.AuthorizeAsync(User, MediaPermissions.ViewMediaOptions)) return Forbid();
        var value = options.Value;
        return Ok(new MediaOptionsDto(value.SupportedSizes, value.AllowedFileExtensions.OrderBy(x => x), value.MaxBrowserCacheDays, value.MaxSecureFilesBrowserCacheDays, value.MaxCacheDays, value.MaxFileSize, value.MaxUploadChunkSize, value.CdnBaseUrl, value.AssetsRequestPath, value.AssetsPath, value.AssetsUsersFolder, value.UseTokenizedQueryString));
    }
}
public sealed record MediaOptionsDto(int[] SupportedSizes, IEnumerable<string> AllowedFileExtensions, int MaxBrowserCacheDays, int MaxSecureFilesBrowserCacheDays, int MaxCacheDays, long MaxFileSize, int? MaxUploadChunkSize, string CdnBaseUrl, string AssetsRequestPath, string AssetsPath, string AssetsUsersFolder, bool UseTokenizedQueryString);
