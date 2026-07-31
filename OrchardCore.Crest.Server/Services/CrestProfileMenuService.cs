using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using OrchardCore.AdminMenu.Services;
using OrchardCore.Navigation;
using Crest.Controllers;
using Crest.Icons;

namespace Crest.Services;

// Resolves every enabled CrestMenuPlacement.User AdminMenu document (the "User Profile
// Menu"s created via AdminMenus.razor) into the same NavigationMenu shape
// NavigationController.GetMenu already returns for "admin". Unlike "admin", these menus
// are NOT INavigationProvider-registered - AdminMenuNavigationProvidersCoordinator only
// builds menu name "adminMenu" and only for AdminMenu.Enabled documents, and
// User-placement menus are deliberately kept Enabled = false (see
// CrestMenuPlacementService) so that coordinator never picks them up. This service is the
// one place that builds User-placement menus directly from their AdminNode trees instead.
//
// There can be MORE THAN ONE User-placement menu, by design: different menus are gated by
// different node-level permissions (e.g. one for editors, one for admins), and a user who
// has access to several should see the union of everything they're authorized for - the
// same way OrchardCore's own admin sidebar merges every enabled AdminMenu document into
// one tree (AdminMenuNavigationProvidersCoordinator.BuildNavigationAsync, upstream OrchardCore.AdminMenu
// module: it iterates ALL enabled menus into one shared NavigationBuilder, then
// permission-filters the merged result once). This service mirrors that exact
// merge-then-filter shape rather than picking a single menu.
//
// Shared by NavigationController (standalone fetch/refresh) and AppController (manifest).
public sealed class CrestProfileMenuService(
    IAdminMenuService adminMenuService,
    CrestMenuPlacementService menuPlacementService,
    IEnumerable<IAdminNodeNavigationBuilder> nodeBuilders,
    IAuthorizationService authorizationService,
    CrestIconController iconController)
{
    public async Task<NavigationMenu> BuildAsync(ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var list = await adminMenuService.GetAdminMenuListAsync();
        var placements = await menuPlacementService.GetAllAsync();
        var profileMenuIds = placements
            .Where(entry => entry.Value.Placement == CrestMenuPlacement.User && entry.Value.Enabled)
            .Select(entry => entry.Key);

        var builder = new NavigationBuilder();
        foreach (var menuId in profileMenuIds)
        {
            var tree = adminMenuService.GetAdminMenuById(list, menuId);
            if (tree is null || tree.MenuItems.Count == 0)
            {
                continue;
            }

            foreach (var node in tree.MenuItems)
            {
                var nodeBuilder = nodeBuilders.FirstOrDefault(candidate => candidate.Name == node.GetType().Name);
                if (nodeBuilder is not null)
                {
                    await nodeBuilder.BuildNavigationAsync(node, builder, nodeBuilders);
                }
            }
        }

        var menuItems = await AuthorizeAsync(builder.Build(), user);
        menuItems = ComputeHref(menuItems);

        var menu = new NavigationMenu(
            "profile",
            menuItems.OrderBy(item => item.Position, NavigationPositionComparer.Instance)
                .Select(NavigationItem.From)
                .ToArray());

        return await iconController.ResolveMenuIconsAsync(menu, null, cancellationToken);
    }

    // Minimal re-implementation of the permission-filter/href-compute steps
    // NavigationManager.BuildMenuAsync performs internally (those helpers are private
    // there) - only what this service needs, since it doesn't go through
    // INavigationManager at all (see comment above BuildAsync).
    private async Task<List<MenuItem>> AuthorizeAsync(List<MenuItem> items, ClaimsPrincipal user)
    {
        var filtered = new List<MenuItem>();
        foreach (var item in items)
        {
            var isAuthorized = true;
            foreach (var permission in item.Permissions)
            {
                if (!await authorizationService.AuthorizeAsync(user, permission, item.Resource))
                {
                    isAuthorized = false;
                    break;
                }
            }

            if (isAuthorized)
            {
                item.Items = await AuthorizeAsync(item.Items, user);
                filtered.Add(item);
            }
        }

        return filtered;
    }

    private static List<MenuItem> ComputeHref(List<MenuItem> items)
    {
        foreach (var item in items)
        {
            if (string.IsNullOrEmpty(item.Href))
            {
                item.Href = string.IsNullOrEmpty(item.Url) ? "#" : item.Url;
            }

            item.Items = ComputeHref(item.Items);
        }

        return items;
    }
}
