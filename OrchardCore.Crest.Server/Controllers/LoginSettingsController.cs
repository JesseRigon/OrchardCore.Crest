using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrchardCore.Environment.Shell;
using OrchardCore.Entities;
using OrchardCore.Settings;
using OrchardCore.Users;
using OrchardCore.Users.Models;

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

public sealed record CrestLoginSettings(
    bool AllowRememberMe,
    bool AllowChangingUsername,
    bool AllowChangingEmail,
    bool AllowChangingPhoneNumber,
    bool UseSiteTheme,
    bool DisableLocalLogin,
    bool RequireTwoFactorAuthentication,
    bool AllowRememberClientTwoFactorAuthentication,
    int NumberOfRecoveryCodesToGenerate,
    bool UseSiteThemeForTwoFactorAuthentication,
    bool UseExternalProviderIfOnlyOneDefined,
    bool UseScriptToSyncProperties,
    string? SyncPropertiesScript)
{
    public static CrestLoginSettings From(LoginSettings login, TwoFactorLoginSettings twoFactor, ExternalLoginSettings external) => new(
        login.AllowRememberMe, login.AllowChangingUsername, login.AllowChangingEmail, login.AllowChangingPhoneNumber,
        login.UseSiteTheme, login.DisableLocalLogin,
        twoFactor.RequireTwoFactorAuthentication, twoFactor.AllowRememberClientTwoFactorAuthentication,
        twoFactor.NumberOfRecoveryCodesToGenerate, twoFactor.UseSiteTheme,
        external.UseExternalProviderIfOnlyOneDefined, external.UseScriptToSyncProperties, external.SyncPropertiesScript);

    public LoginSettings ToLoginSettings() => new()
    {
        AllowRememberMe = AllowRememberMe, AllowChangingUsername = AllowChangingUsername,
        AllowChangingEmail = AllowChangingEmail, AllowChangingPhoneNumber = AllowChangingPhoneNumber,
        UseSiteTheme = UseSiteTheme, DisableLocalLogin = DisableLocalLogin,
    };

    public TwoFactorLoginSettings ToTwoFactorLoginSettings() => new()
    {
        RequireTwoFactorAuthentication = RequireTwoFactorAuthentication,
        AllowRememberClientTwoFactorAuthentication = AllowRememberClientTwoFactorAuthentication,
        NumberOfRecoveryCodesToGenerate = NumberOfRecoveryCodesToGenerate,
        UseSiteTheme = UseSiteThemeForTwoFactorAuthentication,
    };

    public ExternalLoginSettings ToExternalLoginSettings() => new()
    {
        UseExternalProviderIfOnlyOneDefined = UseExternalProviderIfOnlyOneDefined,
        UseScriptToSyncProperties = UseScriptToSyncProperties,
        SyncPropertiesScript = SyncPropertiesScript,
    };
}
