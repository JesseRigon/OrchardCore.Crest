using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrchardCore.Admin;
using Crest.Services;

namespace Crest.Controllers;

[ApiController]
[AutoValidateAntiforgeryToken]
[Route("api/crest/title-bar-settings")]
public sealed class TitleBarSettingsController(
    IAuthorizationService authorizationService,
    CrestTitleBarSettingsStore store) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<CrestTitleBarSettingsDto>> GetAsync()
    {
        if (!await authorizationService.AuthorizeAsync(User, AdminPermissions.ManageAdminSettings)) return Forbid();
        return Ok(CrestTitleBarSettingsDto.From(await store.GetAsync(HttpContext.RequestAborted)));
    }

    [HttpPut]
    public async Task<ActionResult<CrestTitleBarSettingsDto>> PutAsync([FromBody] CrestTitleBarSettingsUpdate update)
    {
        if (!await authorizationService.AuthorizeAsync(User, AdminPermissions.ManageAdminSettings)) return Forbid();
        var saved = await store.SaveAsync(new CrestTitleBarSettings
        {
            DisplayCultureLabel = update.DisplayCultureLabel,
            TenantAvatarImageUrl = update.TenantAvatarImageUrl,
            TenantAvatarShape = update.TenantAvatarShape,
            TenantAvatarClipPath = update.TenantAvatarClipPath,
            TenantAvatarBorderRadius = update.TenantAvatarBorderRadius,
        }, HttpContext.RequestAborted);
        return Ok(CrestTitleBarSettingsDto.From(saved));
    }
}

public sealed record CrestTitleBarSettingsUpdate(
    bool DisplayCultureLabel,
    string? TenantAvatarImageUrl,
    string TenantAvatarShape,
    string? TenantAvatarClipPath,
    string? TenantAvatarBorderRadius);
