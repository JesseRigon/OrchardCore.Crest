using BlazingOrchard.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OrchardCore.AdminMenu.AdminNodes;
using OrchardCore.AdminMenu.Models;
using OrchardCore.AdminMenu.Services;
using OrchardCore.Navigation;

namespace BlazingOrchard.Controllers;

[ApiController]
[IgnoreAntiforgeryToken]
[Route("api/blazing/admin-menus")]
public sealed class AdminMenusController(
    IAuthorizationService authorizationService,
    IAdminMenuService adminMenuService,
    INavigationManager navigationManager,
    BlazingAdminMenuLayoutService layoutService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<AdminMenusState>> ListAsync()
    {
        if (!await authorizationService.AuthorizeAsync(User, OrchardCore.AdminMenu.AdminMenuPermissions.ManageAdminMenu))
        {
            return Forbid();
        }

        var list = await adminMenuService.GetAdminMenuListAsync();
        var defaultMenu = await GetDefaultMenuSummaryAsync();
        return Ok(new AdminMenusState([defaultMenu, .. list.AdminMenu.Select(AdminMenuSummary.From)]));
    }

    [HttpGet("{menuId}")]
    public async Task<ActionResult<AdminMenuSummary>> GetAsync(string menuId)
    {
        if (!await authorizationService.AuthorizeAsync(User, OrchardCore.AdminMenu.AdminMenuPermissions.ManageAdminMenu))
        {
            return Forbid();
        }

        if (menuId == BlazingAdminMenuLayoutService.DefaultMenuId)
        {
            return Ok(await GetDefaultMenuSummaryAsync());
        }

        var list = await adminMenuService.GetAdminMenuListAsync();
        var menu = adminMenuService.GetAdminMenuById(list, menuId);

        return menu is null ? NotFound() : Ok(AdminMenuSummary.From(menu));
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

        var menu = new AdminMenu { Name = name, Enabled = model.Enabled };
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

        if (menuId == BlazingAdminMenuLayoutService.DefaultMenuId)
        {
            return BadRequest("The Sidebar menu cannot be hidden.");
        }

        var list = await adminMenuService.LoadAdminMenuListAsync();
        var menu = adminMenuService.GetAdminMenuById(list, menuId);
        if (menu is null)
        {
            return NotFound();
        }

        menu.Enabled = !menu.Enabled;
        await adminMenuService.SaveAsync(menu);
        return Ok(AdminMenuSummary.From(menu));
    }

    [HttpPost("{menuId}/duplicate")]
    public async Task<ActionResult<AdminMenuSummary>> DuplicateMenuAsync(string menuId)
    {
        if (!await authorizationService.AuthorizeAsync(User, OrchardCore.AdminMenu.AdminMenuPermissions.ManageAdminMenu))
        {
            return Forbid();
        }

        if (menuId == BlazingAdminMenuLayoutService.DefaultMenuId)
        {
            return BadRequest("The default admin menu cannot be duplicated.");
        }

        var list = await adminMenuService.GetAdminMenuListAsync();
        var source = adminMenuService.GetAdminMenuById(list, menuId);
        if (source is null)
        {
            return NotFound();
        }

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
        return Ok(AdminMenuSummary.From(copy));
    }

    [HttpDelete("{menuId}")]
    public async Task<IActionResult> DeleteMenuAsync(string menuId)
    {
        if (!await authorizationService.AuthorizeAsync(User, OrchardCore.AdminMenu.AdminMenuPermissions.ManageAdminMenu))
        {
            return Forbid();
        }

        if (menuId == BlazingAdminMenuLayoutService.DefaultMenuId)
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
        return NoContent();
    }

    [HttpPost("{menuId}/nodes")]
    public async Task<ActionResult<AdminMenuSummary>> CreateNodeAsync(string menuId, AdminMenuNodeEditModel model)
    {
        if (!await authorizationService.AuthorizeAsync(User, OrchardCore.AdminMenu.AdminMenuPermissions.ManageAdminMenu))
        {
            return Forbid();
        }

        if (menuId == BlazingAdminMenuLayoutService.DefaultMenuId)
        {
            var baseMenu = await BuildDefaultNavigationMenuAsync();
            await layoutService.CreateCustomAsync(baseMenu, model.Text.Trim(), model.Url?.Trim(), model.IconClass?.Trim(), model.ParentNodeId, model.Position);
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

    [HttpPut("{menuId}/nodes/{nodeId}")]
    public async Task<ActionResult<AdminMenuSummary>> UpdateNodeAsync(string menuId, string nodeId, AdminMenuNodeEditModel model)
    {
        if (!await authorizationService.AuthorizeAsync(User, OrchardCore.AdminMenu.AdminMenuPermissions.ManageAdminMenu))
        {
            return Forbid();
        }

        if (menuId == BlazingAdminMenuLayoutService.DefaultMenuId)
        {
            var baseMenu = await BuildDefaultNavigationMenuAsync();
            if (!await layoutService.IsCustomAsync(nodeId))
            {
                return BadRequest("Only custom Sidebar nodes can be edited.");
            }

            await layoutService.UpdateCustomAsync(baseMenu, nodeId, model.Text.Trim(), model.Url?.Trim(), model.IconClass?.Trim());
            if (model.ParentNodeId is not null)
            {
                await layoutService.MoveAsync(baseMenu, nodeId, model.ParentNodeId, model.Position);
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

        if (menuId != BlazingAdminMenuLayoutService.DefaultMenuId)
        {
            return BadRequest("Sidebar renames are only supported on the generated Sidebar menu.");
        }

        var baseMenu = await BuildDefaultNavigationMenuAsync();
        await layoutService.RenameAsync(baseMenu, nodeId, model.Text);
        return Ok(await GetDefaultMenuSummaryAsync());
    }

    [HttpPost("{menuId}/nodes/{nodeId}/move")]
    public async Task<ActionResult<AdminMenuSummary>> MoveNodeAsync(string menuId, string nodeId, AdminMenuNodeMoveModel model)
    {
        if (!await authorizationService.AuthorizeAsync(User, OrchardCore.AdminMenu.AdminMenuPermissions.ManageAdminMenu))
        {
            return Forbid();
        }

        if (menuId == BlazingAdminMenuLayoutService.DefaultMenuId)
        {
            var baseMenu = await BuildDefaultNavigationMenuAsync();
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

        if (menuId == BlazingAdminMenuLayoutService.DefaultMenuId)
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

    [HttpDelete("{menuId}/nodes/{nodeId}")]
    public async Task<ActionResult<AdminMenuSummary>> DeleteNodeAsync(string menuId, string nodeId)
    {
        if (!await authorizationService.AuthorizeAsync(User, OrchardCore.AdminMenu.AdminMenuPermissions.ManageAdminMenu))
        {
            return Forbid();
        }

        if (menuId == BlazingAdminMenuLayoutService.DefaultMenuId)
        {
            var baseMenu = await BuildDefaultNavigationMenuAsync();
            if (!await layoutService.IsCustomAsync(nodeId))
            {
                return BadRequest("Only custom Sidebar nodes can be deleted.");
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
        return Ok(AdminMenuSummary.From(menu));
    }

    private async Task<AdminMenuSummary> GetDefaultMenuSummaryAsync()
    {
        var layout = await layoutService.GetAsync();
        var baseMenu = await BuildDefaultNavigationMenuAsync();
        var items = layoutService.ApplyForManagement(baseMenu.Items, layout);
        return new AdminMenuSummary(
            BlazingAdminMenuLayoutService.DefaultMenuId,
            BlazingAdminMenuLayoutService.DefaultMenuName,
            true,
            true,
            items.Select((item, index) => AdminMenuNodeSummary.From(item, layout, layoutService, null, 0, index)).ToArray());
    }

    private async Task<NavigationMenu> BuildDefaultNavigationMenuAsync()
    {
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
    AdminMenuNodeSummary[] Nodes)
{
    public static AdminMenuSummary From(AdminMenu menu) => new(
        menu.Id,
        menu.Name,
        menu.Enabled,
        false,
        menu.MenuItems.OfType<AdminNode>().Select((node, index) => AdminMenuNodeSummary.From(node, null, 0, index)).ToArray());
}

public sealed record AdminMenuNodeSummary(
    string Id,
    string Type,
    string Text,
    string? Url,
    string? IconClass,
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

    public static AdminMenuNodeSummary From(NavigationItem item, BlazingAdminMenuLayoutDocument layout, BlazingAdminMenuLayoutService layoutService, string? parentId, int depth, int order)
    {
        var itemOverride = layout.Items.FirstOrDefault(layoutItem => string.Equals(layoutItem.ItemKey, item.Key, StringComparison.Ordinal));
        var originalText = !string.IsNullOrWhiteSpace(itemOverride?.DisplayText) && !string.Equals(itemOverride.Text, item.Text, StringComparison.Ordinal)
            ? itemOverride.Text
            : null;

        return new(
            item.Key,
            item.Items.Length > 0 ? nameof(PlaceholderAdminNode) : nameof(LinkAdminNode),
            item.Text,
            item.Link,
            GetIconClass(item),
            !layoutService.IsHidden(layout, item.Key),
            0,
            item.Position,
            parentId,
            depth,
            order,
            [],
            item.Key.StartsWith("custom-", StringComparison.Ordinal) == true,
            originalText,
            item.Items.Select((child, index) => From(child, layout, layoutService, item.Key, depth + 1, index)).ToArray());
    }

    private static string[] GetPermissionNames(AdminNode node) => node switch
    {
        LinkAdminNode link => link.PermissionNames,
        PlaceholderAdminNode placeholder => placeholder.PermissionNames,
        _ => [],
    };

    private static string? GetIconClass(NavigationItem item)
    {
        var iconClasses = item.Classes
            .Where(className => className.StartsWith("icon-class-", StringComparison.OrdinalIgnoreCase))
            .Select(className => className["icon-class-".Length..])
            .Where(className => !string.IsNullOrWhiteSpace(className))
            .ToArray();

        return iconClasses.Length == 0 ? null : string.Join(" ", iconClasses);
    }
}

public sealed record AdminMenuEditModel(string? Name, bool Enabled);

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
