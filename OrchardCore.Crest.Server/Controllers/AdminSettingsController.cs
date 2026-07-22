using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrchardCore.Admin;
using OrchardCore.Admin.Models;
using OrchardCore.Entities;
using OrchardCore.Settings;

namespace Crest.Controllers;

[ApiController]
[AutoValidateAntiforgeryToken]
[Route("api/crest/admin-settings")]
public sealed class AdminSettingsController(
    IAuthorizationService authorizationService,
    ISiteService siteService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<AdminSettingsDto>> GetAsync()
    {
        if (!await authorizationService.AuthorizeAsync(User, AdminPermissions.ManageAdminSettings))
        {
            return Forbid();
        }

        var site = await siteService.GetSiteSettingsAsync();
        return Ok(AdminSettingsDto.From(site.GetOrCreate<AdminSettings>()));
    }

    [HttpPut]
    public async Task<ActionResult<AdminSettingsDto>> PutAsync(AdminSettingsUpdate update)
    {
        if (!await authorizationService.AuthorizeAsync(User, AdminPermissions.ManageAdminSettings))
        {
            return Forbid();
        }

        var site = await siteService.LoadSiteSettingsAsync();
        var updated = new AdminSettings
        {
            DisplayThemeToggler = update.DisplayThemeToggler,
            DisplayMenuFilter = update.DisplayMenuFilter,
            DisplayNewMenu = update.DisplayNewMenu,
            DisplayTitlesInTopbar = update.DisplayTitlesInTopbar,
        };

        site.Alter<AdminSettings>(settings =>
        {
            settings.DisplayThemeToggler = updated.DisplayThemeToggler;
            settings.DisplayMenuFilter = updated.DisplayMenuFilter;
            settings.DisplayNewMenu = updated.DisplayNewMenu;
            settings.DisplayTitlesInTopbar = updated.DisplayTitlesInTopbar;
        });

        await siteService.UpdateSiteSettingsAsync(site);

        return Ok(AdminSettingsDto.From(updated));
    }
}

public sealed record AdminSettingsUpdate(
    bool DisplayThemeToggler,
    bool DisplayMenuFilter,
    bool DisplayNewMenu,
    bool DisplayTitlesInTopbar);
