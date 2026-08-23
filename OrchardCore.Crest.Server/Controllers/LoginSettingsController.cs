using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrchardCore.Environment.Shell;
using OrchardCore.Entities;
using OrchardCore.Settings;
using OrchardCore.Users;
using OrchardCore.Users.Models;
using Crest.ViewModels;

namespace Crest.Controllers;

[ApiController]
[AutoValidateAntiforgeryToken]
[Route("api/crest/settings/login")]
public sealed class LoginSettingsController(ISiteService sites, IShellReleaseManager releaseManager, IAuthorizationService authorization) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<CrestLoginSettings>> GetAsync()
    {
        if (!await authorization.AuthorizeAsync(User, UsersPermissions.ManageUsers)) return Forbid();
        return Ok(CrestLoginSettings.From(
            await sites.GetSettingsAsync<LoginSettings>(),
            await sites.GetSettingsAsync<TwoFactorLoginSettings>(),
            await sites.GetSettingsAsync<ExternalLoginSettings>()));
    }

    [HttpPut]
    public async Task<ActionResult<CrestLoginSettings>> SaveAsync([FromBody] CrestLoginSettings request)
    {
        if (!await authorization.AuthorizeAsync(User, UsersPermissions.ManageUsers)) return Forbid();
        if (request.NumberOfRecoveryCodesToGenerate < 1)
            return BadRequest("The number of recovery codes must be at least one.");

        var site = await sites.LoadSiteSettingsAsync();
        site.Alter<LoginSettings>(settings => Copy(request.ToLoginSettings(), settings));
        site.Alter<TwoFactorLoginSettings>(settings => Copy(request.ToTwoFactorLoginSettings(), settings));
        site.Alter<ExternalLoginSettings>(settings => Copy(request.ToExternalLoginSettings(), settings));
        await sites.UpdateSiteSettingsAsync(site);

        // These settings participate in authentication and must take effect for the next request.
        releaseManager.RequestRelease();
        return Ok(request);
    }

    private static void Copy<T>(T source, T destination)
    {
        foreach (var property in typeof(T).GetProperties().Where(property => property.CanRead && property.CanWrite))
            property.SetValue(destination, property.GetValue(source));
    }
}
