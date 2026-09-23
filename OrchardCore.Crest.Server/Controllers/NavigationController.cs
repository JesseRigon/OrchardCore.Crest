using Crest.Services;
using Crest.Icons;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OrchardCore.Admin;
using OrchardCore.Navigation;
using Crest.ViewModels;

namespace Crest.Controllers;

[ApiController]
[AutoValidateAntiforgeryToken]
[Route("api/crest/navigation")]
public sealed class NavigationController(
    ICrestRequestAccess requestAccess) : ControllerBase
{
    [HttpGet("admin")]
    public Task<ActionResult<NavigationMenu>> GetAdminMenu() => GetMenu("admin");

    // Resolves the tenant's single CrestMenuPlacement.User AdminMenu document into a
    // NavigationMenu. See CrestProfileMenuService for why this can't go through
    // INavigationManager.BuildMenuAsync the way "admin" does.
    [HttpGet("profile")]
    public async Task<ActionResult<NavigationMenu>> GetProfileMenuAsync()
    {
        var access = await requestAccess.AuthorizeAsync(User, AdminPermissions.AccessAdminPanel);
        if (access is null)
        {
            return Forbid();
        }

        var profileMenuService = access.GetRequiredService<CrestProfileMenuService>();
        return Ok(await profileMenuService.BuildAsync(User, HttpContext.RequestAborted));
    }

    // Self-service, like api/crest/localization/me: the current user's own hidden admin
    // menu items. Not a management endpoint - there is no user id in the route.
    [HttpGet("me/hidden")]
    public async Task<ActionResult<CrestUserHiddenMenuItems>> GetMyHiddenItemsAsync()
    {
        var access = await requestAccess.AuthorizeAsync(User, AdminPermissions.AccessAdminPanel);
        if (access is null)
        {
            return Forbid();
        }

        var preferences = await access.GetRequiredService<CrestUserMenuPreferencesService>().GetAsync(User);
        return Ok(new CrestUserHiddenMenuItems(preferences.HiddenItemKeys.ToArray()));
    }

    [HttpPut("me/hidden")]
    public async Task<ActionResult<CrestUserHiddenMenuItems>> SetMyHiddenItemsAsync(CrestUserHiddenMenuItems request)
    {
        var access = await requestAccess.AuthorizeAsync(User, AdminPermissions.AccessAdminPanel);
        if (access is null)
        {
            return Forbid();
        }

        var saved = await access.GetRequiredService<CrestUserMenuPreferencesService>().SetHiddenItemKeysAsync(User, request.HiddenItemKeys ?? []);
        return saved is null
            ? StatusCode(StatusCodes.Status500InternalServerError)
            : Ok(new CrestUserHiddenMenuItems(saved.HiddenItemKeys.ToArray()));
    }

    [HttpGet("menus/{menuName}")]
    public async Task<ActionResult<NavigationMenu>> GetMenu(string menuName)
    {
        var access = await requestAccess.AuthorizeAsync(User, AdminPermissions.AccessAdminPanel);
        if (access is null)
        {
            return Forbid();
        }

        var primaryNavMenuSettingsStore = access.GetRequiredService<CrestPrimaryNavMenuSettingsStore>();
        var iconController = access.GetRequiredService<CrestIconController>();

        NavigationMenu menu;
        if (string.Equals(menuName, CrestAdminMenuBuilder.MenuName, StringComparison.OrdinalIgnoreCase))
        {
            await access.GetRequiredService<CrestAdminSettingsNormalizer>().EnsureNewMenuEnabledAsync();
            menu = await access.GetRequiredService<CrestAdminMenuBuilder>().BuildAsync(ControllerContext, User);
            menu = menu with { PrimaryNavMenuSettings = await primaryNavMenuSettingsStore.GetAsync(HttpContext.RequestAborted) };
        }
        else
        {
            var items = await access.GetRequiredService<INavigationManager>().BuildMenuAsync(menuName, ControllerContext);
            var captionResolver = access.GetRequiredService<CrestMenuCaptionResolver>();
            await captionResolver.EnsureLoadedAsync();
            menu = new NavigationMenu(
                menuName,
                items.OrderBy(item => item.Position, NavigationPositionComparer.Instance)
                    .Select(item => NavigationItem.From(item, captionResolver))
                    .ToArray());
        }

        return Ok(await iconController.ResolveMenuIconsAsync(
            menu,
            string.Equals(menuName, "admin", StringComparison.OrdinalIgnoreCase) ? CrestIconController.AdminMenuChromeIconKeys : null,
            HttpContext.RequestAborted));
    }
}
