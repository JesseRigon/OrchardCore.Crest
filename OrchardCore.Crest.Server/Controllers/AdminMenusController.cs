using System.Globalization;
using Crest.Icons;
using Crest.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OrchardCore.AdminMenu.AdminNodes;
using OrchardCore.AdminMenu.Models;
using OrchardCore.AdminMenu.Services;
using OrchardCore.Navigation;

namespace Crest.Controllers;

[ApiController]
[AutoValidateAntiforgeryToken]
[Route("api/crest/admin-menus")]
public sealed class AdminMenusController(
    IAuthorizationService authorizationService,
    IAdminMenuService adminMenuService,
    INavigationManager navigationManager,
    CrestAdminMenuLayoutService layoutService,
    CrestAdminMenuTranslationService translationService,
    CrestProviderMenuSyncCoordinator providerMenuSync,
    CrestProviderMenuSyncService providerMenuSyncService,
    CrestMenuPlacementService menuPlacementService,
    CrestPrimaryNavMenuSettingsStore primaryNavMenuSettingsStore,
    CrestAdminSettingsNormalizer adminSettingsNormalizer,
    CrestIconController iconController) : ControllerBase
{
    // Re-runs the provider-menu import on demand. The import normally runs once per shell on
    // first use; this exposes it for the case where a tenant needs it re-checked without waiting
    // for a shell release, and reports what actually changed.
    [HttpPost("sync-providers")]
    public async Task<ActionResult<CrestProviderMenuSyncResult>> SyncProvidersAsync()
    {
        if (!await authorizationService.AuthorizeAsync(User, OrchardCore.AdminMenu.AdminMenuPermissions.ManageAdminMenu))
        {
            return Forbid();
        }

        // reseedMissingTranslations: an admin invoking the sync by hand is asking for
        // restoration - missing PO-seeded translations are refilled even if seeded before,
        // which is also the recovery path for seeds lost to the Translations editor's
        // wholesale save. The automatic per-shell pass never refills a deleted one.
        return Ok(await providerMenuSyncService.SyncAsync(ControllerContext, reseedMissingTranslations: true));
    }

    [HttpGet]
    public async Task<ActionResult<AdminMenusState>> ListAsync()
    {
        if (!await authorizationService.AuthorizeAsync(User, OrchardCore.AdminMenu.AdminMenuPermissions.ManageAdminMenu))
        {
            return Forbid();
        }

        var list = await adminMenuService.GetAdminMenuListAsync();
        var defaultMenu = await GetDefaultMenuSummaryAsync();
        var placements = await menuPlacementService.GetAllAsync();
        return Ok(new AdminMenusState([defaultMenu, .. list.AdminMenu.Select(menu => WithPlacement(AdminMenuSummary.From(menu), placements))]));
    }

    [HttpGet("{menuId}")]
    public async Task<ActionResult<AdminMenuSummary>> GetAsync(string menuId)
    {
        if (!await authorizationService.AuthorizeAsync(User, OrchardCore.AdminMenu.AdminMenuPermissions.ManageAdminMenu))
        {
            return Forbid();
        }

        if (menuId == CrestAdminMenuLayoutService.DefaultMenuId)
        {
            return Ok(await GetDefaultMenuSummaryAsync());
        }

        var list = await adminMenuService.GetAdminMenuListAsync();
        var menu = adminMenuService.GetAdminMenuById(list, menuId);
        if (menu is null)
        {
            return NotFound();
        }

        var entry = await menuPlacementService.GetAsync(menuId);
        return Ok(WithPlacement(AdminMenuSummary.From(menu), entry));
    }

    [HttpPost]
    public async Task<ActionResult<AdminMenuSummary>> CreateMenuAsync(AdminMenuEditModel model)
    {
        if (!await authorizationService.AuthorizeAsync(User, OrchardCore.AdminMenu.AdminMenuPermissions.ManageAdminMenu))
        {
            return Forbid();
        }

        var name = model.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return BadRequest("Menu name is required.");
        }

        // OrchardCore's own admin-menu coordinator unconditionally injects every enabled
        // custom menu into the "admin" sidebar tree — it has no concept of placement. Menus
        // that aren't Admin-placed stay Enabled=false here forever so that coordinator never
        // picks them up; CrestMenuPlacementEntry.Enabled is the "real" visible/hidden flag
        // for those, independent of this one.
        var placement = model.Placement;
        var menu = new AdminMenu { Name = name, Enabled = placement == CrestMenuPlacement.Admin && model.Enabled };
        await adminMenuService.SaveAsync(menu);

        if (placement != CrestMenuPlacement.Admin)
        {
            await menuPlacementService.SetAsync(menu.Id, placement, model.Enabled);
        }

        return Ok(WithPlacement(AdminMenuSummary.From(menu), placement, model.Enabled));
    }

    [HttpPost("{menuId}/convert")]
    public async Task<ActionResult<AdminMenuSummary>> ConvertMenuAsync(string menuId, ConvertMenuModel model)
    {
        if (!await authorizationService.AuthorizeAsync(User, OrchardCore.AdminMenu.AdminMenuPermissions.ManageAdminMenu))
        {
            return Forbid();
        }

        if (menuId == CrestAdminMenuLayoutService.DefaultMenuId)
        {
            return BadRequest("The Sidebar (Built In) menu cannot be converted.");
        }

        if (model.Placement == CrestMenuPlacement.User)
        {
            return BadRequest("Menus cannot be converted to a User Profile Menu — create a new one instead.");
        }

        var list = await adminMenuService.GetAdminMenuListAsync();
        var menu = adminMenuService.GetAdminMenuById(list, menuId);
        if (menu is null)
        {
            return NotFound();
        }

        var currentEntry = await menuPlacementService.GetAsync(menuId);
        if (currentEntry.Placement == CrestMenuPlacement.User)
        {
            return BadRequest("A User Profile Menu cannot be converted to another type.");
        }

        if (currentEntry.Placement == model.Placement)
        {
            return Ok(WithPlacement(AdminMenuSummary.From(menu), currentEntry));
        }

        var enabled = currentEntry.Enabled;
        if (model.Placement == CrestMenuPlacement.Admin)
        {
            menu.Enabled = enabled;
            await adminMenuService.SaveAsync(menu);
            await menuPlacementService.RemoveAsync(menuId);
            return Ok(WithPlacement(AdminMenuSummary.From(menu), CrestMenuPlacement.Admin, enabled));
        }

        menu.Enabled = false;
        await adminMenuService.SaveAsync(menu);
        await menuPlacementService.SetAsync(menuId, model.Placement, enabled);
        return Ok(WithPlacement(AdminMenuSummary.From(menu), model.Placement, enabled));
    }

    [HttpPost("{menuId}/rename")]
    public async Task<ActionResult<AdminMenuSummary>> RenameMenuAsync(string menuId, AdminMenuEditModel model)
    {
        if (!await authorizationService.AuthorizeAsync(User, OrchardCore.AdminMenu.AdminMenuPermissions.ManageAdminMenu))
        {
            return Forbid();
        }

        if (menuId == CrestAdminMenuLayoutService.DefaultMenuId)
        {
            return BadRequest("The Primary Navigation menu cannot be renamed.");
        }

        var name = model.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return BadRequest("Menu name is required.");
        }

        var list = await adminMenuService.LoadAdminMenuListAsync();
        var menu = adminMenuService.GetAdminMenuById(list, menuId);
        if (menu is null)
        {
            return NotFound();
        }

        menu.Name = name;
        foreach (var node in menu.MenuItems.OfType<AdminNode>())
        {
            SetMenuName(node, name);
        }

        await adminMenuService.SaveAsync(menu);
        return Ok(AdminMenuSummary.From(menu));
    }

    [HttpPost("{menuId}/toggle")]
    public async Task<ActionResult<AdminMenuSummary>> ToggleMenuAsync(string menuId)
    {
        if (!await authorizationService.AuthorizeAsync(User, OrchardCore.AdminMenu.AdminMenuPermissions.ManageAdminMenu))
        {
            return Forbid();
        }

        if (menuId == CrestAdminMenuLayoutService.DefaultMenuId)
        {
            return BadRequest("The primary navigation menu cannot be hidden.");
        }

        var list = await adminMenuService.LoadAdminMenuListAsync();
        var menu = adminMenuService.GetAdminMenuById(list, menuId);
        if (menu is null)
        {
            return NotFound();
        }

        var entry = await menuPlacementService.GetAsync(menuId);
        if (entry.Placement == CrestMenuPlacement.Admin)
        {
            menu.Enabled = !menu.Enabled;
            await adminMenuService.SaveAsync(menu);
            return Ok(WithPlacement(AdminMenuSummary.From(menu), entry));
        }

        // AdminMenu.Enabled stays false for non-Admin placements (see CreateMenuAsync) —
        // the placement entry's own Enabled is the real visible/hidden flag here.
        var enabled = !entry.Enabled;
        await menuPlacementService.SetAsync(menuId, entry.Placement, enabled);
        return Ok(WithPlacement(AdminMenuSummary.From(menu), entry.Placement, enabled));
    }

    [HttpPost("{menuId}/duplicate")]
    public async Task<ActionResult<AdminMenuSummary>> DuplicateMenuAsync(string menuId)
    {
        if (!await authorizationService.AuthorizeAsync(User, OrchardCore.AdminMenu.AdminMenuPermissions.ManageAdminMenu))
        {
            return Forbid();
        }

        if (menuId == CrestAdminMenuLayoutService.DefaultMenuId)
        {
            return BadRequest("The default admin menu cannot be duplicated.");
        }

        var list = await adminMenuService.GetAdminMenuListAsync();
        var source = adminMenuService.GetAdminMenuById(list, menuId);
        if (source is null)
        {
            return NotFound();
        }

        var sourceEntry = await menuPlacementService.GetAsync(menuId);
        var copy = new AdminMenu
        {
            Name = $"{source.Name} Copy",
            Enabled = source.Enabled,
        };

        foreach (var node in source.MenuItems.OfType<AdminNode>())
        {
            copy.MenuItems.Add(CloneNode(node, copy.Name));
        }

        await adminMenuService.SaveAsync(copy);
        if (sourceEntry.Placement != CrestMenuPlacement.Admin)
        {
            await menuPlacementService.SetAsync(copy.Id, sourceEntry.Placement, sourceEntry.Enabled);
        }

        return Ok(WithPlacement(AdminMenuSummary.From(copy), sourceEntry));
    }

    [HttpDelete("{menuId}")]
    public async Task<IActionResult> DeleteMenuAsync(string menuId)
    {
        if (!await authorizationService.AuthorizeAsync(User, OrchardCore.AdminMenu.AdminMenuPermissions.ManageAdminMenu))
        {
            return Forbid();
        }

        if (menuId == CrestAdminMenuLayoutService.DefaultMenuId)
        {
            return BadRequest("The default admin menu cannot be deleted.");
        }

        var list = await adminMenuService.GetAdminMenuListAsync();
        var menu = adminMenuService.GetAdminMenuById(list, menuId);
        if (menu is null)
        {
            return NotFound();
        }

        await adminMenuService.DeleteAsync(menu);
        await menuPlacementService.RemoveAsync(menuId);

        // The menu's translations go with it: its whole context, plus the menu NAME's own
        // entry in the generic context (that one only if no other menu still bears the name -
        // menu names are not enforced unique).
        await translationService.RemoveContextAsync(OrchardCore.AdminMenu.DataLocalizationContext.AdminMenu(menu.Name));
        var nameStillUsed = list.AdminMenu.Any(other =>
            other.Id != menu.Id && string.Equals(other.Name, menu.Name, StringComparison.OrdinalIgnoreCase));
        if (!nameStillUsed)
        {
            await translationService.RemoveKeysAsync(
                OrchardCore.AdminMenu.DataLocalizationContext.AdminMenu(),
                [menu.Name]);
        }

        return NoContent();
    }

    [HttpPost("{menuId}/nodes")]
    public async Task<ActionResult<AdminMenuSummary>> CreateNodeAsync(string menuId, AdminMenuNodeEditModel model)
    {
        if (!await authorizationService.AuthorizeAsync(User, OrchardCore.AdminMenu.AdminMenuPermissions.ManageAdminMenu))
        {
            return Forbid();
        }

        if (menuId == CrestAdminMenuLayoutService.DefaultMenuId)
        {
            if (!IsPlaceholderNodeType(model.Type))
            {
                return BadRequest("Primary Navigation only supports custom placeholder nodes.");
            }

            var baseMenu = await BuildDefaultNavigationMenuAsync();
            if (await layoutService.IsLockedNewBranchAsync(baseMenu, model.ParentNodeId))
            {
                return BadRequest("The New menu branch is locked and cannot be edited.");
            }

            await layoutService.CreateCustomAsync(baseMenu, model.Text.Trim(), null, model.IconClass?.Trim(), model.ParentNodeId, model.Position);
            return Ok(await GetDefaultMenuSummaryAsync());
        }

        var menu = await LoadMenuForUpdateAsync(menuId);
        if (menu is null)
        {
            return NotFound();
        }

        var node = CreateNode(model.Type);
        if (node is null)
        {
            return BadRequest("Unsupported admin menu node type.");
        }

        node.UniqueId = Guid.NewGuid().ToString("n");
        node.MenuName = menu.Name;
        ApplyNode(node, model);

        if (!TryGetParent(menu, model.ParentNodeId, out var parent))
        {
            return BadRequest("The selected parent admin menu node was not found.");
        }

        var position = ClampPosition(model.Position ?? GetChildCount(menu, parent), GetChildCount(menu, parent));

        if (!menu.InsertMenuItemAt(node, parent, position))
        {
            return StatusCode(StatusCodes.Status500InternalServerError);
        }

        await adminMenuService.SaveAsync(menu);
        return Ok(AdminMenuSummary.From(menu));
    }

    [HttpPost("{menuId}/separators")]
    public async Task<ActionResult<AdminMenuSummary>> CreateSeparatorAsync(string menuId, AdminMenuSeparatorEditModel model)
    {
        if (!await authorizationService.AuthorizeAsync(User, OrchardCore.AdminMenu.AdminMenuPermissions.ManageAdminMenu))
        {
            return Forbid();
        }

        if (menuId != CrestAdminMenuLayoutService.DefaultMenuId)
        {
            return BadRequest("Separators are currently supported by the Primary Navigation menu only.");
        }

        var baseMenu = await BuildDefaultNavigationMenuAsync();
        if (await layoutService.IsLockedNewBranchAsync(baseMenu, model.ParentNodeId))
        {
            return BadRequest("The New menu branch is locked and cannot be edited.");
        }

        await layoutService.CreateSeparatorAsync(baseMenu, model.ParentNodeId, model.Position);
        return Ok(await GetDefaultMenuSummaryAsync());
    }

    [HttpPost("{menuId}/primary-nav-menu-settings")]
    public async Task<ActionResult<AdminMenuSummary>> UpdatePrimaryNavMenuSettingsAsync(string menuId, [FromBody] CrestPrimaryNavMenuSettings settings)
    {
        if (!await authorizationService.AuthorizeAsync(User, OrchardCore.AdminMenu.AdminMenuPermissions.ManageAdminMenu))
        {
            return Forbid();
        }

        if (menuId != CrestAdminMenuLayoutService.DefaultMenuId)
        {
            return BadRequest("Primary navigation settings are supported by the Primary Navigation menu only.");
        }

        var normalized = await primaryNavMenuSettingsStore.SaveAsync(settings, HttpContext.RequestAborted);
        return Ok((await GetDefaultMenuSummaryAsync()) with { PrimaryNavMenuSettings = normalized });
    }

    [HttpDelete("{menuId}/separators/{separatorId}")]
    public async Task<ActionResult<AdminMenuSummary>> DeleteSeparatorAsync(string menuId, string separatorId)
    {
        if (!await authorizationService.AuthorizeAsync(User, OrchardCore.AdminMenu.AdminMenuPermissions.ManageAdminMenu))
        {
            return Forbid();
        }

        if (menuId != CrestAdminMenuLayoutService.DefaultMenuId)
        {
            return BadRequest("Separators are currently supported by the Primary Navigation menu only.");
        }

        await layoutService.DeleteSeparatorAsync(await BuildDefaultNavigationMenuAsync(), separatorId);
        return Ok(await GetDefaultMenuSummaryAsync());
    }

    [HttpPost("{menuId}/separators/{separatorId}/move")]
    public async Task<ActionResult<AdminMenuSummary>> MoveSeparatorAsync(string menuId, string separatorId, AdminMenuSeparatorEditModel model)
    {
        if (!await authorizationService.AuthorizeAsync(User, OrchardCore.AdminMenu.AdminMenuPermissions.ManageAdminMenu))
        {
            return Forbid();
        }

        if (menuId != CrestAdminMenuLayoutService.DefaultMenuId)
        {
            return BadRequest("Separators are currently supported by the Primary Navigation menu only.");
        }

        var baseMenu = await BuildDefaultNavigationMenuAsync();
        if (await layoutService.IsLockedNewBranchAsync(baseMenu, model.ParentNodeId))
        {
            return BadRequest("The New menu branch is locked and cannot be edited.");
        }

        await layoutService.MoveSeparatorAsync(baseMenu, separatorId, model.ParentNodeId, model.Position);
        return Ok(await GetDefaultMenuSummaryAsync());
    }

    [HttpPut("{menuId}/nodes/{nodeId}")]
    public async Task<ActionResult<AdminMenuSummary>> UpdateNodeAsync(string menuId, string nodeId, AdminMenuNodeEditModel model)
    {
        if (!await authorizationService.AuthorizeAsync(User, OrchardCore.AdminMenu.AdminMenuPermissions.ManageAdminMenu))
        {
            return Forbid();
        }

        if (menuId == CrestAdminMenuLayoutService.DefaultMenuId)
        {
            var baseMenu = await BuildDefaultNavigationMenuAsync();
            var editingNewBranch = await layoutService.IsLockedNewBranchAsync(baseMenu, nodeId);
            if (!editingNewBranch && await layoutService.IsLockedNewBranchAsync(baseMenu, model.ParentNodeId))
            {
                return BadRequest("The New menu branch has a fixed structure and cannot accept moved items.");
            }

            if (await layoutService.IsCustomAsync(nodeId))
            {
                await layoutService.UpdateCustomAsync(baseMenu, nodeId, model.Text.Trim(), null, model.IconClass?.Trim());
                if (!editingNewBranch && model.ParentNodeId is not null)
                {
                    await layoutService.MoveAsync(baseMenu, nodeId, model.ParentNodeId, model.Position);
                }
            }
            else
            {
                await layoutService.UpdateItemAsync(
                    baseMenu,
                    nodeId,
                    model.Text,
                    model.IconClass,
                    editingNewBranch ? null : model.ParentNodeId,
                    editingNewBranch ? null : model.Position);
            }

            return Ok(await GetDefaultMenuSummaryAsync());
        }

        var menu = await LoadMenuForUpdateAsync(menuId);
        if (menu is null)
        {
            return NotFound();
        }

        var node = menu.GetMenuItemById(nodeId);
        if (node is null)
        {
            return NotFound();
        }

        node.MenuName = menu.Name;
        ApplyNode(node, model);

        if (model.ParentNodeId is not null)
        {
            if (!MoveNode(menu, node, model.ParentNodeId, model.Position))
            {
                return StatusCode(StatusCodes.Status500InternalServerError);
            }
        }

        await adminMenuService.SaveAsync(menu);
        return Ok(AdminMenuSummary.From(menu));
    }

    [HttpPost("{menuId}/nodes/{nodeId}/rename")]
    public async Task<ActionResult<AdminMenuSummary>> RenameNodeAsync(string menuId, string nodeId, AdminMenuNodeRenameModel model)
    {
        if (!await authorizationService.AuthorizeAsync(User, OrchardCore.AdminMenu.AdminMenuPermissions.ManageAdminMenu))
        {
            return Forbid();
        }

        if (menuId != CrestAdminMenuLayoutService.DefaultMenuId)
        {
            return BadRequest("Primary navigation renames are only supported on the generated Primary Navigation menu.");
        }

        var baseMenu = await BuildDefaultNavigationMenuAsync();
        await layoutService.RenameAsync(baseMenu, nodeId, model.Text);
        return Ok(await GetDefaultMenuSummaryAsync());
    }

    // Promotes a rename from the Crest layout document into the tenant's translation store,
    // making it the tenant-wide translation of that caption rather than a Crest-only display
    // override. Separate from the rename endpoint above, and separately authorized: renaming an
    // item in Crest's own sidebar only needs ManageAdminMenu, but writing the tenant's
    // translation store changes what Orchard's Razor admin renders for every user of this
    // tenant in that culture, so it additionally requires ManageTranslations (Administrator
    // only by default - see OrchardCore.DataLocalization's Permissions).
    [HttpPost("{menuId}/nodes/{nodeId}/promote-rename")]
    public async Task<ActionResult<AdminMenuSummary>> PromoteRenameAsync(string menuId, string nodeId)
    {
        if (!await authorizationService.AuthorizeAsync(User, OrchardCore.AdminMenu.AdminMenuPermissions.ManageAdminMenu) ||
            !await authorizationService.AuthorizeAsync(User, OrchardCore.Localization.Data.DataLocalizationPermissions.ManageTranslations))
        {
            return Forbid();
        }

        var culture = CultureInfo.CurrentUICulture.Name;
        if (string.IsNullOrEmpty(culture))
        {
            // The invariant culture has no translation store entry to write to - a translation
            // has to be attributed to a culture. Callers hit this only when the tenant resolved
            // to invariant, which dev.sh and the Crest recipes now avoid by always configuring
            // a real default culture.
            return BadRequest("A rename can only be promoted while viewing a specific culture.");
        }

        // Resolved from the owning admin menu rather than from the generated primary navigation.
        // Not every admin menu feeds that navigation, so looking the node up there would make
        // promotion unreachable for menus that don't - and the node's own menu is where both
        // pieces this needs actually live:
        //   * the caption Orchard looks the translation up by, and
        //   * the menu name that scopes the IDataLocalizer context (see TheAdmin's
        //     NavigationItemText.cshtml, which keys on the item's own MenuName).
        var owner = await FindNodeAsync(nodeId);
        if (owner is null)
        {
            // Provider-contributed items have no owning AdminMenu, so there is no DB-backed
            // caption for IDataLocalizer to translate. Their captions come from the provider's
            // own S["..."] resource and are translated through PO files instead, which a tenant
            // admin cannot edit from here.
            return BadRequest("Only items from an admin menu can be promoted; this caption comes from a module's own resources.");
        }

        var (menuName, sourceText) = owner.Value;
        if (string.IsNullOrWhiteSpace(sourceText))
        {
            return NotFound();
        }

        // Follows whatever the layout currently says for this culture, including "nothing":
        // promoting after a rename has been cleared removes the translation, so the two stores
        // can be brought back into agreement the same way they were put into it.
        var layout = await layoutService.LoadAsync();
        var renamed = CrestAdminMenuLayoutService.GetRename(layout, nodeId, culture);

        await translationService.SetAsync(culture, menuName, sourceText, renamed);
        return Ok(await GetDefaultMenuSummaryAsync());
    }

    [HttpPost("{menuId}/nodes/{nodeId}/move")]
    public async Task<ActionResult<AdminMenuSummary>> MoveNodeAsync(string menuId, string nodeId, AdminMenuNodeMoveModel model)
    {
        if (!await authorizationService.AuthorizeAsync(User, OrchardCore.AdminMenu.AdminMenuPermissions.ManageAdminMenu))
        {
            return Forbid();
        }

        if (menuId == CrestAdminMenuLayoutService.DefaultMenuId)
        {
            var baseMenu = await BuildDefaultNavigationMenuAsync();
            if (await layoutService.IsLockedNewBranchAsync(baseMenu, nodeId) || await layoutService.IsLockedNewBranchAsync(baseMenu, model.ParentNodeId))
            {
                return BadRequest("The New menu branch is locked and cannot be edited.");
            }

            await layoutService.MoveAsync(baseMenu, nodeId, model.ParentNodeId, model.Position);
            return Ok(await GetDefaultMenuSummaryAsync());
        }

        var menu = await LoadMenuForUpdateAsync(menuId);
        if (menu is null)
        {
            return NotFound();
        }

        var node = menu.GetMenuItemById(nodeId);
        if (node is null)
        {
            return NotFound();
        }

        if (!MoveNode(menu, node, model.ParentNodeId, model.Position))
        {
            return StatusCode(StatusCodes.Status500InternalServerError);
        }

        await adminMenuService.SaveAsync(menu);
        return Ok(AdminMenuSummary.From(menu));
    }

    [HttpPost("{menuId}/nodes/{nodeId}/toggle")]
    public async Task<ActionResult<AdminMenuSummary>> ToggleNodeAsync(string menuId, string nodeId)
    {
        if (!await authorizationService.AuthorizeAsync(User, OrchardCore.AdminMenu.AdminMenuPermissions.ManageAdminMenu))
        {
            return Forbid();
        }

        if (menuId == CrestAdminMenuLayoutService.DefaultMenuId)
        {
            var baseMenu = await BuildDefaultNavigationMenuAsync();
            await layoutService.ToggleAsync(baseMenu, nodeId);
            return Ok(await GetDefaultMenuSummaryAsync());
        }

        var menu = await LoadMenuForUpdateAsync(menuId);
        if (menu is null)
        {
            return NotFound();
        }

        var node = menu.GetMenuItemById(nodeId);
        if (node is null)
        {
            return NotFound();
        }

        node.Enabled = !node.Enabled;
        await adminMenuService.SaveAsync(menu);
        return Ok(AdminMenuSummary.From(menu));
    }

    // Removes stored layout overrides that no longer match anything in the CURRENT menu
    // tree - e.g. rows left behind by the fixed Href-based Key bug (see
    // NavigationItem.Key in NavigationController.cs). Only safe to run when all features
    // are enabled: with any feature disabled, that feature's items are also absent from
    // baseMenu and their still-legitimate overrides would be wrongly deleted, since
    // "disabled but installed" and "not present at all" both look identical here.
    [HttpPost("{menuId}/prune-overrides")]
    public async Task<ActionResult<AdminMenuSummary>> PruneOverridesAsync(string menuId)
    {
        if (!await authorizationService.AuthorizeAsync(User, OrchardCore.AdminMenu.AdminMenuPermissions.ManageAdminMenu))
        {
            return Forbid();
        }

        if (menuId != CrestAdminMenuLayoutService.DefaultMenuId)
        {
            return NotFound();
        }

        var baseMenu = await BuildDefaultNavigationMenuAsync();
        await layoutService.PruneOrphanedOverridesAsync(baseMenu);
        return Ok(await GetDefaultMenuSummaryAsync());
    }

    [HttpDelete("{menuId}/nodes/{nodeId}")]
    public async Task<ActionResult<AdminMenuSummary>> DeleteNodeAsync(string menuId, string nodeId)
    {
        if (!await authorizationService.AuthorizeAsync(User, OrchardCore.AdminMenu.AdminMenuPermissions.ManageAdminMenu))
        {
            return Forbid();
        }

        if (menuId == CrestAdminMenuLayoutService.DefaultMenuId)
        {
            var baseMenu = await BuildDefaultNavigationMenuAsync();
            if (await layoutService.IsLockedNewBranchAsync(baseMenu, nodeId))
            {
                return BadRequest("The New menu branch is locked and cannot be edited.");
            }

            if (!await layoutService.IsCustomAsync(nodeId))
            {
                return BadRequest("Only custom primary navigation nodes can be deleted.");
            }

            await layoutService.DeleteCustomAsync(baseMenu, nodeId);
            return Ok(await GetDefaultMenuSummaryAsync());
        }

        var menu = await LoadMenuForUpdateAsync(menuId);
        if (menu is null)
        {
            return NotFound();
        }

        var node = menu.GetMenuItemById(nodeId);
        if (node is null)
        {
            return NotFound();
        }

        if (!menu.RemoveMenuItem(node))
        {
            return StatusCode(StatusCodes.Status500InternalServerError);
        }

        await adminMenuService.SaveAsync(menu);

        // The node's translations must not outlive it as orphans. The deleted subtree's
        // captions are removed from the menu's translation context in every culture - except
        // any caption a surviving node in the same menu still uses, since translations are
        // keyed on the caption and shared by every node bearing it.
        var deletedCaptions = new HashSet<string>(StringComparer.Ordinal);
        CollectNodeCaptions([node], deletedCaptions);
        var survivingCaptions = new HashSet<string>(StringComparer.Ordinal);
        CollectNodeCaptions(menu.MenuItems, survivingCaptions);
        deletedCaptions.ExceptWith(survivingCaptions);
        await translationService.RemoveKeysAsync(
            OrchardCore.AdminMenu.DataLocalizationContext.AdminMenu(menu.Name),
            deletedCaptions);

        return Ok(AdminMenuSummary.From(menu));
    }

    private static void CollectNodeCaptions(IEnumerable<MenuItem> items, HashSet<string> captions)
    {
        foreach (var item in items)
        {
            var caption = item switch
            {
                LinkAdminNode link => link.LinkText,
                PlaceholderAdminNode placeholder => placeholder.LinkText,
                _ => null,
            };

            if (!string.IsNullOrWhiteSpace(caption))
            {
                captions.Add(caption);
            }

            CollectNodeCaptions(item.Items, captions);
        }
    }

    private async Task<AdminMenuSummary> GetDefaultMenuSummaryAsync()
    {
        // LoadAsync (GetOrCreateMutableAsync), not GetAsync (GetOrCreateImmutableAsync).
        // The immutable overload serves the CACHED document, which does not yet reflect
        // layout mutations made earlier in this same request — those are only visible via
        // the mutable instance until the deferred save commits after the response is
        // written. Every mutating endpoint returns this summary, so reading the immutable
        // copy made each PUT/POST respond with the PREVIOUS state: the write landed, but
        // the response body was always one edit behind.
        var layout = await layoutService.LoadAsync();
        var primaryNavMenuSettings = await primaryNavMenuSettingsStore.GetAsync(HttpContext.RequestAborted);
        var baseMenu = await BuildDefaultNavigationMenuAsync();
        // Captured BEFORE ApplyForManagement substitutes any DisplayText rename, so the
        // editor can show "originally called X" without the layout ever storing X.
        var originalTexts = AdminMenuNodeSummary.CollectOriginalTexts(baseMenu.Items);
        var managedMenu = await iconController.ResolveMenuIconsAsync(baseMenu with
        {
            Items = layoutService.ApplyForManagement(baseMenu.Items, layout),
        }, CrestIconController.AdminMenuChromeIconKeys, HttpContext.RequestAborted);
        var items = managedMenu.Items;
        var separators = layout.Separators
            .Where(separator => !string.IsNullOrWhiteSpace(separator.Key))
            .Select(separator => new AdminMenuSeparatorSummary(
                separator.Key,
                string.IsNullOrWhiteSpace(separator.ParentKey) ? null : separator.ParentKey,
                0,
                separator.Order))
            .ToArray();
        return new AdminMenuSummary(
            CrestAdminMenuLayoutService.DefaultMenuId,
            CrestAdminMenuLayoutService.DefaultMenuName,
            true,
            true,
            separators,
            primaryNavMenuSettings,
            managedMenu.Icons,
            items.Select((item, index) => AdminMenuNodeSummary.From(item, layout, layoutService, null, 0, index, originalTexts)).ToArray());
    }

    // The owning admin menu's name and the node's own stored caption, for the node with this
    // UniqueId - or null when no admin menu owns it (a provider-contributed item). Matched on
    // UniqueId rather than caption, because that is exactly the identity a caption cannot supply
    // once it has been translated or edited.
    private async Task<(string MenuName, string? SourceText)?> FindNodeAsync(string nodeId)
    {
        var list = await adminMenuService.GetAdminMenuListAsync();
        foreach (var menu in list.AdminMenu)
        {
            var found = FindNode(menu.MenuItems, nodeId);
            if (found is not null)
            {
                return (menu.Name, AdminMenuNodeSummary.GetNodeText(found));
            }
        }

        return null;

        static AdminNode? FindNode(IEnumerable<MenuItem> items, string nodeId)
        {
            foreach (var item in items)
            {
                if (item is AdminNode node && string.Equals(node.UniqueId, nodeId, StringComparison.Ordinal))
                {
                    return node;
                }

                var child = FindNode(item.Items, nodeId);
                if (child is not null)
                {
                    return child;
                }
            }

            return null;
        }
    }

    private async Task<NavigationMenu> BuildDefaultNavigationMenuAsync()
    {
        await adminSettingsNormalizer.EnsureNewMenuEnabledAsync();
        // Once per shell, before the menu is read, so provider items exist as admin menu nodes.
        // Both the provider item and its imported node contribute to the same "admin" menu;
        // NavigationManager.Merge folds each pair into one item, and because the imported node
        // carries Priority+1, Merge's authority rule hands the survivor the node's values -
        // including its UniqueId as the item's Id.
        await providerMenuSync.EnsureSyncedAsync(ControllerContext);
        var items = await navigationManager.BuildMenuAsync("admin", ControllerContext);
        return new NavigationMenu("admin", items.OrderBy(item => item.Position, NavigationPositionComparer.Instance)
            .Select(NavigationItem.From)
            .ToArray());
    }

    private async Task<AdminMenu?> LoadMenuForUpdateAsync(string menuId)
    {
        var list = await adminMenuService.LoadAdminMenuListAsync();
        return adminMenuService.GetAdminMenuById(list, menuId);
    }

    private static AdminMenuSummary WithPlacement(AdminMenuSummary summary, CrestMenuPlacementEntry entry) =>
        WithPlacement(summary, entry.Placement, entry.Enabled);

    private static AdminMenuSummary WithPlacement(AdminMenuSummary summary, IReadOnlyDictionary<string, CrestMenuPlacementEntry> placements) =>
        placements.TryGetValue(summary.Id, out var entry) ? WithPlacement(summary, entry) : summary;

    private static AdminMenuSummary WithPlacement(AdminMenuSummary summary, CrestMenuPlacement placement, bool enabled) =>
        placement == CrestMenuPlacement.Admin ? summary : summary with { Placement = placement, Enabled = enabled };

    private static AdminNode CloneNode(AdminNode node, string menuName)
    {
        var clone = node switch
        {
            LinkAdminNode link => (AdminNode)new LinkAdminNode
            {
                LinkText = link.LinkText,
                LinkUrl = link.LinkUrl,
                IconClass = link.IconClass,
                PermissionNames = link.PermissionNames.ToArray(),
            },
            PlaceholderAdminNode placeholder => (AdminNode)new PlaceholderAdminNode
            {
                LinkText = placeholder.LinkText,
                IconClass = placeholder.IconClass,
                PermissionNames = placeholder.PermissionNames.ToArray(),
            },
            _ => new PlaceholderAdminNode { LinkText = node.Text.Value },
        };

        clone.UniqueId = Guid.NewGuid().ToString("n");
        clone.MenuName = menuName;
        clone.Enabled = node.Enabled;
        clone.Priority = node.Priority;
        clone.Position = node.Position;

        foreach (var child in node.Items.OfType<AdminNode>())
        {
            clone.Items.Add(CloneNode(child, menuName));
        }

        return clone;
    }

    private static bool IsPlaceholderNodeType(string type) =>
        string.Equals(type, nameof(PlaceholderAdminNode), StringComparison.OrdinalIgnoreCase) ||
        string.Equals(type, "placeholder", StringComparison.OrdinalIgnoreCase);

    private static void SetMenuName(AdminNode node, string menuName)
    {
        node.MenuName = menuName;
        foreach (var child in node.Items.OfType<AdminNode>())
        {
            SetMenuName(child, menuName);
        }
    }

    private static AdminNode? CreateNode(string type) => type switch
    {
        nameof(LinkAdminNode) or "link" => new LinkAdminNode(),
        nameof(PlaceholderAdminNode) or "placeholder" => new PlaceholderAdminNode(),
        _ => null,
    };

    private static void ApplyNode(AdminNode node, AdminMenuNodeEditModel model)
    {
        node.Enabled = model.Enabled;
        node.Priority = model.Priority;
        node.Position = model.DisplayPosition;

        switch (node)
        {
            case LinkAdminNode link:
                link.LinkText = model.Text.Trim();
                link.LinkUrl = model.Url?.Trim() ?? string.Empty;
                link.IconClass = model.IconClass?.Trim() ?? string.Empty;
                link.PermissionNames = NormalizePermissionNames(model.PermissionNames);
                break;
            case PlaceholderAdminNode placeholder:
                placeholder.LinkText = model.Text.Trim();
                placeholder.IconClass = model.IconClass?.Trim() ?? string.Empty;
                placeholder.PermissionNames = NormalizePermissionNames(model.PermissionNames);
                break;
        }
    }

    private static bool MoveNode(AdminMenu menu, AdminNode node, string? parentNodeId, int? requestedPosition)
    {
        if (node.UniqueId == parentNodeId || IsDescendantOf(node, parentNodeId))
        {
            return false;
        }

        if (!TryGetParent(menu, parentNodeId, out var parent))
        {
            return false;
        }

        var oldParent = FindParent(menu, node);
        var oldIndex = GetNodeIndex(menu, oldParent, node);
        var position = requestedPosition ?? GetChildCount(menu, parent);

        if (ReferenceEquals(oldParent, parent) && oldIndex >= 0 && position > oldIndex)
        {
            position--;
        }

        if (!menu.RemoveMenuItem(node))
        {
            return false;
        }

        position = ClampPosition(position, GetChildCount(menu, parent));
        return menu.InsertMenuItemAt(node, parent, position);
    }

    private static bool IsDescendantOf(AdminNode node, string? candidateId)
    {
        if (string.IsNullOrWhiteSpace(candidateId))
        {
            return false;
        }

        foreach (var child in node.Items.OfType<AdminNode>())
        {
            if (child.UniqueId == candidateId || IsDescendantOf(child, candidateId))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryGetParent(AdminMenu menu, string? parentNodeId, out AdminNode? parent)
    {
        parent = null;
        if (string.IsNullOrWhiteSpace(parentNodeId))
        {
            return true;
        }

        parent = menu.GetMenuItemById(parentNodeId);
        return parent is not null;
    }

    private static AdminNode? FindParent(AdminMenu menu, AdminNode node)
    {
        if (menu.MenuItems.Contains(node))
        {
            return null;
        }

        foreach (var root in menu.MenuItems.OfType<AdminNode>())
        {
            var parent = FindParent(root, node);
            if (parent is not null)
            {
                return parent;
            }
        }

        return null;
    }

    private static AdminNode? FindParent(AdminNode parent, AdminNode node)
    {
        if (parent.Items.Contains(node))
        {
            return parent;
        }

        foreach (var child in parent.Items.OfType<AdminNode>())
        {
            var found = FindParent(child, node);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    private static int GetNodeIndex(AdminMenu menu, AdminNode? parent, AdminNode node)
        => parent is null ? menu.MenuItems.IndexOf(node) : parent.Items.IndexOf(node);

    private static int GetChildCount(AdminMenu menu, AdminNode? parent)
        => parent is null ? menu.MenuItems.Count : parent.Items.OfType<AdminNode>().Count();

    private static int ClampPosition(int position, int childCount)
        => Math.Clamp(position, 0, childCount);

    private static string[] NormalizePermissionNames(string[]? permissionNames)
        => permissionNames?.Select(x => x.Trim()).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray() ?? [];
}

public sealed record AdminMenusState(AdminMenuSummary[] Menus);

public sealed record AdminMenuSummary(
    string Id,
    string Name,
    bool Enabled,
    bool IsDefault,
    AdminMenuSeparatorSummary[] Separators,
    CrestPrimaryNavMenuSettings PrimaryNavMenuSettings,
    IconPack? Icons,
    AdminMenuNodeSummary[] Nodes,
    CrestMenuPlacement Placement = CrestMenuPlacement.Admin)
{
    public static AdminMenuSummary From(AdminMenu menu) => new(
        menu.Id,
        menu.Name,
        menu.Enabled,
        false,
        [],
        CrestPrimaryNavMenuSettings.Default,
        null,
        menu.MenuItems.OfType<AdminNode>().Select((node, index) => AdminMenuNodeSummary.From(node, null, 0, index)).ToArray());
}

public sealed record AdminMenuSeparatorSummary(
    string Id,
    string? ParentId,
    int Depth,
    int Order);

public sealed record AdminMenuNodeSummary(
    string Id,
    string Type,
    string Text,
    string? Url,
    string? IconClass,
    NavigationIcon? Icon,
    bool Enabled,
    int Priority,
    string? DisplayPosition,
    string? ParentId,
    int Depth,
    int Order,
    string[] PermissionNames,
    bool IsCustom,
    string? OriginalText,
    AdminMenuNodeSummary[] Items)
{
    public static AdminMenuNodeSummary From(AdminNode node, string? parentId, int depth, int order) => new(
        node.UniqueId,
        node.GetType().Name,
        GetText(node),
        node is LinkAdminNode link ? link.LinkUrl : null,
        GetIconClass(node),
        null,
        node.Enabled,
        node.Priority,
        node.Position,
        parentId,
        depth,
        order,
        GetPermissionNames(node),
        false,
        null,
        node.Items.OfType<AdminNode>().Select((child, index) => From(child, node.UniqueId, depth + 1, index)).ToArray());

    // The node's own stored caption, as opposed to the rendered one: this is the key the
    // translation store is looked up by.
    public static string GetNodeText(AdminNode node) => GetText(node);

    private static string GetText(AdminNode node) => node switch
    {
        LinkAdminNode link => link.LinkText,
        PlaceholderAdminNode placeholder => placeholder.LinkText,
        _ => node.Text.Value,
    };

    private static string? GetIconClass(AdminNode node) => node switch
    {
        LinkAdminNode link => link.IconClass,
        PlaceholderAdminNode placeholder => placeholder.IconClass,
        _ => null,
    };

    public static AdminMenuNodeSummary From(NavigationItem item, CrestAdminMenuLayoutDocument layout, CrestAdminMenuLayoutService layoutService, string? parentId, int depth, int order, IReadOnlyDictionary<string, string> originalTextsById)
    {
        var itemOverride = layout.Items.FirstOrDefault(layoutItem => string.Equals(layoutItem.ItemKey, item.Key, StringComparison.Ordinal));
        // The "originally called X" hint shown next to a renamed item. Resolved by Id from
        // the pre-override menu tree, whose captions Orchard just localized for THIS
        // request - never from a stored copy. The layout document is identity-only by
        // design: persisting a caption there would bake whatever culture the renaming
        // admin happened to be using into the tenant's layout, and from there into its
        // exported recipe. item.Text is unusable as the original here because Build() has
        // already substituted DisplayText over it by the time this runs.
        // Renames are per-culture, so the hint must only appear when this culture actually has
        // one: a Spanish rename shouldn't mark the item as renamed while viewing any other
        // culture.
        var originalText = !string.IsNullOrWhiteSpace(itemOverride?.GetDisplayText(CultureInfo.CurrentUICulture.Name))
            && originalTextsById.TryGetValue(item.Key, out var original)
            && !string.Equals(original, item.Text, StringComparison.Ordinal)
            ? original
            : null;

        return new(
            item.Key,
            item.Items.Length > 0 || string.IsNullOrWhiteSpace(item.Link) ? nameof(PlaceholderAdminNode) : nameof(LinkAdminNode),
            item.Text,
            item.Link,
            GetIconClass(item),
            item.Icon,
            !layoutService.IsHidden(layout, item.Key),
            0,
            item.Position,
            parentId,
            depth,
            order,
            [],
            item.Key.StartsWith("custom-", StringComparison.Ordinal) == true,
            originalText,
            item.Items.Select((child, index) => From(child, layout, layoutService, item.Key, depth + 1, index, originalTextsById)).ToArray());
    }

    // Id -> the item's own, un-overridden caption as Orchard localized it for this
    // request. Built from the base menu before any layout override is applied.
    public static Dictionary<string, string> CollectOriginalTexts(IEnumerable<NavigationItem> items)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        Collect(items);
        return map;

        void Collect(IEnumerable<NavigationItem> nodes)
        {
            foreach (var node in nodes)
            {
                if (!string.IsNullOrWhiteSpace(node.Key))
                {
                    map[node.Key] = node.Text;
                }

                Collect(node.Items);
            }
        }
    }

    private static string[] GetPermissionNames(AdminNode node) => node switch
    {
        LinkAdminNode link => link.PermissionNames,
        PlaceholderAdminNode placeholder => placeholder.PermissionNames,
        _ => [],
    };

    private static string? GetIconClass(NavigationItem item)
    {
        var iconClasses = GetIconClasses(item.Classes);
        if (iconClasses.Length > 0)
        {
            return string.Join(" ", iconClasses);
        }

        return item.Icon is null ? null : GetIconClass(item.Icon);
    }

    private static string? GetIconClass(NavigationIcon icon)
    {
        if (string.IsNullOrWhiteSpace(icon.Name))
        {
            return null;
        }

        return icon.Key;
    }

    private static string[] GetIconClasses(string[] classes)
    {
        var hasIconMarker = false;
        var iconClasses = new List<string>();

        foreach (var className in classes.SelectMany(value => value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)))
        {
            if (className.StartsWith("icon-class-", StringComparison.OrdinalIgnoreCase))
            {
                hasIconMarker = true;
                var iconClass = className["icon-class-".Length..];
                if (!string.IsNullOrWhiteSpace(iconClass))
                {
                    iconClasses.Add(iconClass);
                }

                continue;
            }

            if (hasIconMarker)
            {
                iconClasses.Add(className);
            }
        }

        return hasIconMarker ? iconClasses.Distinct(StringComparer.OrdinalIgnoreCase).ToArray() : [];
    }
}

public sealed record AdminMenuEditModel(string? Name, bool Enabled, CrestMenuPlacement Placement = CrestMenuPlacement.Admin);

public sealed record ConvertMenuModel(CrestMenuPlacement Placement);

public sealed record AdminMenuNodeEditModel(
    string Type,
    string Text,
    string? Url,
    string? IconClass,
    bool Enabled,
    int Priority,
    string? DisplayPosition,
    string[]? PermissionNames,
    string? ParentNodeId,
    int? Position);

public sealed record AdminMenuNodeMoveModel(string? ParentNodeId, int? Position);

public sealed record AdminMenuNodeRenameModel(string? Text);

public sealed record AdminMenuSeparatorEditModel(string? ParentNodeId, int? Position);
