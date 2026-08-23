using Crest.Services;
using Crest.Icons;
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

    [HttpGet("menus/{menuName}")]
    public async Task<ActionResult<NavigationMenu>> GetMenu(string menuName)
    {
        var access = await requestAccess.AuthorizeAsync(User, AdminPermissions.AccessAdminPanel);
        if (access is null)
        {
            return Forbid();
        }

        var navigationManager = access.GetRequiredService<INavigationManager>();
        var layoutService = access.GetRequiredService<CrestAdminMenuLayoutService>();
        var primaryNavMenuSettingsStore = access.GetRequiredService<CrestPrimaryNavMenuSettingsStore>();
        var adminSettingsNormalizer = access.GetRequiredService<CrestAdminSettingsNormalizer>();
        var iconController = access.GetRequiredService<CrestIconController>();

        // Orchard builds, authorizes, and reduces this tree for the actual
        // request user. Apply the tenant-wide Crest layout only afterwards.
        if (string.Equals(menuName, "admin", StringComparison.OrdinalIgnoreCase))
        {
            await adminSettingsNormalizer.EnsureNewMenuEnabledAsync();
            // Once per shell: import provider items as admin menu nodes (see the note in
            // AdminMenusController's BuildDefaultNavigationMenuAsync for how Merge then keys the
            // rendered items by UniqueId). Done here as well so the import happens on whichever
            // of the sidebar or the menu editor is loaded first.
            await access.GetRequiredService<CrestProviderMenuSyncCoordinator>().EnsureSyncedAsync(ControllerContext);
        }

        var items = await navigationManager.BuildMenuAsync(menuName, ControllerContext);

        // Resolves each admin menu node's caption against the tenant translation store for the
        // request culture - see NavigationItem.From and CrestMenuCaptionResolver for the
        // MenuName restoration and hierarchical context fallback. With the data localization
        // feature absent the resolver leaves captions untouched, so the menu renders rather
        // than fails.
        var captionResolver = access.GetRequiredService<CrestMenuCaptionResolver>();
        await captionResolver.EnsureLoadedAsync();

        var menu = new NavigationMenu(
            menuName,
            items.OrderBy(item => item.Position, NavigationPositionComparer.Instance)
                .Select(item => NavigationItem.From(item, captionResolver))
                .ToArray());

        if (string.Equals(menuName, "admin", StringComparison.OrdinalIgnoreCase))
        {
            menu = await layoutService.ApplyAsync(menu);
            menu = menu with { PrimaryNavMenuSettings = await primaryNavMenuSettingsStore.GetAsync(HttpContext.RequestAborted) };
        }

        return Ok(await iconController.ResolveMenuIconsAsync(
            menu,
            string.Equals(menuName, "admin", StringComparison.OrdinalIgnoreCase) ? CrestIconController.AdminMenuChromeIconKeys : null,
            HttpContext.RequestAborted));
    }
}
