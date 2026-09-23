using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using OrchardCore.Navigation;
using Crest.ViewModels;

namespace Crest.Services;

/// <summary>
/// The served admin menu, assembled once here for everyone who needs it: Orchard builds
/// and permission-filters the tree for the request user, the provider sync has keyed its
/// items by node UniqueId, captions resolve through the tenant translation store, the
/// tenant's layout overlay is applied, and - unless a caller needs the tenant view - the
/// user's own hidden items go last. The sidebar endpoint, the app manifest and any page
/// that lists what the menu lists (All Parties) must agree on this tree, so none of them
/// assembles it themselves.
/// </summary>
public sealed class CrestAdminMenuBuilder(
    INavigationManager navigationManager,
    CrestProviderMenuSyncCoordinator syncCoordinator,
    CrestMenuCaptionResolver captionResolver,
    CrestAdminMenuLayoutService layoutService,
    CrestUserMenuPreferencesService userMenuPreferences)
{
    public const string MenuName = "admin";

    public async Task<NavigationMenu> BuildAsync(ActionContext actionContext, ClaimsPrincipal user, bool applyUserPreferences = true)
    {
        // Once per shell: import provider items as admin menu nodes (see the note in
        // AdminMenusController's BuildDefaultNavigationMenuAsync for how Merge then keys the
        // rendered items by UniqueId). Done on first use of whichever consumer loads first.
        await syncCoordinator.EnsureSyncedAsync(actionContext);

        var items = await navigationManager.BuildMenuAsync(MenuName, actionContext);

        // Resolves each admin menu node's caption against the tenant translation store for the
        // request culture - see NavigationItem.From and CrestMenuCaptionResolver for the
        // MenuName restoration and hierarchical context fallback. With the data localization
        // feature absent the resolver leaves captions untouched, so the menu renders rather
        // than fails.
        await captionResolver.EnsureLoadedAsync();

        var menu = new NavigationMenu(
            MenuName,
            items.OrderBy(item => item.Position, NavigationPositionComparer.Instance)
                .Select(item => NavigationItem.From(item, captionResolver))
                .ToArray());

        // Orchard has already authorized and reduced the tree for the request user; the
        // tenant-wide layout applies only afterwards, and the user's own overlay after that.
        menu = await layoutService.ApplyAsync(menu);
        return applyUserPreferences ? await userMenuPreferences.ApplyAsync(menu, user) : menu;
    }
}
