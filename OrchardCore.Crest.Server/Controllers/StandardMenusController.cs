using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OrchardCore;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.Contents;
using OrchardCore.Menu;
using OrchardCore.Menu.Models;
using OrchardContentItem = OrchardCore.ContentManagement.ContentItem;
using Crest.ViewModels;

namespace Crest.Controllers;

[ApiController]
[AutoValidateAntiforgeryToken]
[Route("api/crest/menus")]
public sealed class StandardMenusController(
    IAuthorizationService authorizationService,
    IOrchardHelper orchardHelper,
    IContentManager contentManager,
    IContentDefinitionManager contentDefinitionManager) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<StandardMenusState>> ListAsync()
    {
        if (!await authorizationService.AuthorizeAsync(User, OrchardCore.Menu.Permissions.ManageMenu))
        {
            return Forbid();
        }

        var menus = await orchardHelper.QueryContentItemsAsync(query => query
            .Where(index => index.ContentType == "Menu" && index.Latest)
            .OrderBy(index => index.DisplayText));

        var nodeTypes = await GetAvailableNodeTypesAsync();
        return Ok(new StandardMenusState(menus.Select(menu => StandardMenuSummary.From(menu)).ToArray(), nodeTypes));
    }

    [HttpPost]
    public async Task<ActionResult<StandardMenuSummary>> CreateMenuAsync(StandardMenuEditModel model)
    {
        if (!await IsAuthorizedAsync())
        {
            return Forbid();
        }

        var name = model.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return BadRequest("Menu name is required.");
        }

        var menu = await contentManager.NewAsync("Menu");
        menu.DisplayText = name;
        EnsureMenuItemsList(menu.Content);
        await contentManager.PublishAsync(menu);

        return Ok(StandardMenuSummary.From(menu));
    }

    [HttpPost("{menuId}/rename")]
    public async Task<ActionResult<StandardMenuSummary>> RenameMenuAsync(string menuId, StandardMenuEditModel model)
    {
        if (!await IsAuthorizedAsync())
        {
            return Forbid();
        }

        var name = model.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return BadRequest("Menu name is required.");
        }

        var menu = await LoadMenuForUpdateAsync(menuId);
        if (menu is null)
        {
            return NotFound();
        }

        menu.DisplayText = name;
        await contentManager.PublishAsync(menu);
        return Ok(StandardMenuSummary.From(menu));
    }

    [HttpPost("{menuId}/toggle")]
    public async Task<ActionResult<StandardMenuSummary>> ToggleMenuAsync(string menuId)
    {
        if (!await IsAuthorizedAsync())
        {
            return Forbid();
        }

        var menu = await contentManager.GetAsync(menuId, VersionOptions.Latest);
        if (menu is null)
        {
            return NotFound();
        }

        if (menu.Published)
        {
            await contentManager.UnpublishAsync(menu);
            menu = await contentManager.GetAsync(menuId, VersionOptions.Latest) ?? menu;
        }
        else
        {
            await contentManager.PublishAsync(menu);
        }

        return Ok(StandardMenuSummary.From(menu));
    }

    [HttpPost("{menuId}/duplicate")]
    public async Task<ActionResult<StandardMenuSummary>> DuplicateMenuAsync(string menuId)
    {
        if (!await IsAuthorizedAsync())
        {
            return Forbid();
        }

        var source = await contentManager.GetAsync(menuId, VersionOptions.Latest);
        if (source is null)
        {
            return NotFound();
        }

        var copy = await contentManager.NewAsync("Menu");
        copy.DisplayText = $"{source.DisplayText} Copy";
        copy.Content.Merge(CloneObject(source.Content));
        copy.ContentItemId = string.Empty;
        copy.ContentItemVersionId = string.Empty;
        RegenerateNodeIds(copy.Content);
        await contentManager.PublishAsync(copy);

        return Ok(StandardMenuSummary.From(copy));
    }

    [HttpDelete("{menuId}")]
    public async Task<IActionResult> DeleteMenuAsync(string menuId)
    {
        if (!await IsAuthorizedAsync())
        {
            return Forbid();
        }

        var menu = await contentManager.GetAsync(menuId, VersionOptions.Latest);
        if (menu is null)
        {
            return NotFound();
        }

        await contentManager.RemoveAsync(menu);
        return NoContent();
    }

    [HttpPost("{menuId}/nodes")]
    public async Task<ActionResult<StandardMenuSummary>> CreateNodeAsync(string menuId, StandardMenuNodeEditModel model)
    {
        if (!await IsAuthorizedAsync())
        {
            return Forbid();
        }

        var menu = await LoadMenuForUpdateAsync(menuId);
        if (menu is null)
        {
            return NotFound();
        }

        if (!IsSupportedNodeType(model.Type))
        {
            return BadRequest("Unsupported site menu item type.");
        }

        var node = await CreateNodeObjectAsync(model);
        if (!TryGetChildren((JsonObject)menu.Content, model.ParentNodeId, out var siblings))
        {
            return BadRequest("The selected parent menu item was not found.");
        }

        siblings.Insert(ClampPosition(model.Position, siblings.Count), node);
        await contentManager.PublishAsync(menu);
        return Ok(StandardMenuSummary.From(menu));
    }

    [HttpPut("{menuId}/nodes/{nodeId}")]
    public async Task<ActionResult<StandardMenuSummary>> UpdateNodeAsync(string menuId, string nodeId, StandardMenuNodeEditModel model)
    {
        if (!await IsAuthorizedAsync())
        {
            return Forbid();
        }

        var menu = await LoadMenuForUpdateAsync(menuId);
        if (menu is null)
        {
            return NotFound();
        }

        if (!TryFindNode((JsonObject)menu.Content, nodeId, out var node, out _))
        {
            return NotFound();
        }

        ApplyNode(node, model);
        await contentManager.PublishAsync(menu);
        return Ok(StandardMenuSummary.From(menu));
    }

    [HttpPost("{menuId}/nodes/{nodeId}/move")]
    public async Task<ActionResult<StandardMenuSummary>> MoveNodeAsync(string menuId, string nodeId, StandardMenuNodeMoveModel model)
    {
        if (!await IsAuthorizedAsync())
        {
            return Forbid();
        }

        var menu = await LoadMenuForUpdateAsync(menuId);
        if (menu is null)
        {
            return NotFound();
        }

        if (!TryFindNode((JsonObject)menu.Content, nodeId, out var node, out var sourceArray) || sourceArray is null)
        {
            return NotFound();
        }

        if (model.ParentNodeId == nodeId || IsDescendant(node, model.ParentNodeId))
        {
            return BadRequest("A menu item cannot be moved under itself or one of its children.");
        }

        if (!TryGetChildren((JsonObject)menu.Content, model.ParentNodeId, out var targetArray))
        {
            return BadRequest("The selected parent menu item was not found.");
        }

        sourceArray.Remove(node);
        targetArray.Insert(ClampPosition(model.Position, targetArray.Count), node);
        await contentManager.PublishAsync(menu);
        return Ok(StandardMenuSummary.From(menu));
    }

    [HttpPost("{menuId}/nodes/{nodeId}/duplicate")]
    public async Task<ActionResult<StandardMenuSummary>> DuplicateNodeAsync(string menuId, string nodeId)
    {
        if (!await IsAuthorizedAsync())
        {
            return Forbid();
        }

        var menu = await LoadMenuForUpdateAsync(menuId);
        if (menu is null)
        {
            return NotFound();
        }

        if (!TryFindNode((JsonObject)menu.Content, nodeId, out var node, out var siblings) || siblings is null)
        {
            return NotFound();
        }

        var copy = CloneObject(node);
        copy["DisplayText"] = $"{ReadString(node, "DisplayText") ?? "Menu item"} Copy";
        RegenerateNodeIds(copy);
        siblings.Insert(siblings.IndexOf(node) + 1, copy);
        await contentManager.PublishAsync(menu);
        return Ok(StandardMenuSummary.From(menu));
    }

    [HttpDelete("{menuId}/nodes/{nodeId}")]
    public async Task<ActionResult<StandardMenuSummary>> DeleteNodeAsync(string menuId, string nodeId)
    {
        if (!await IsAuthorizedAsync())
        {
            return Forbid();
        }

        var menu = await LoadMenuForUpdateAsync(menuId);
        if (menu is null)
        {
            return NotFound();
        }

        if (!TryFindNode((JsonObject)menu.Content, nodeId, out var node, out var siblings) || siblings is null)
        {
            return NotFound();
        }

        siblings.Remove(node);
        await contentManager.PublishAsync(menu);
        return Ok(StandardMenuSummary.From(menu));
    }

    private Task<bool> IsAuthorizedAsync() => authorizationService.AuthorizeAsync(User, OrchardCore.Menu.Permissions.ManageMenu);

    private async Task<OrchardContentItem?> LoadMenuForUpdateAsync(string menuId)
    {
        var definition = await contentDefinitionManager.GetTypeDefinitionAsync("Menu");
        return definition?.IsDraftable() == true
            ? await contentManager.GetAsync(menuId, VersionOptions.DraftRequired)
            : await contentManager.GetAsync(menuId, VersionOptions.Latest);
    }

    private async Task<StandardMenuNodeType[]> GetAvailableNodeTypesAsync()
    {
        var definitions = await contentDefinitionManager.ListTypeDefinitionsAsync();
        return definitions
            .Where(type => type.GetStereotype() == "MenuItem" && IsSupportedNodeType(type.Name))
            .Select(type => new StandardMenuNodeType(type.Name, string.IsNullOrWhiteSpace(type.DisplayName) ? type.Name : type.DisplayName))
            .OrderBy(type => type.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static bool IsSupportedNodeType(string? type) =>
        string.Equals(type, "LinkMenuItem", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(type, "HtmlMenuItem", StringComparison.OrdinalIgnoreCase);

    private async Task<JsonObject> CreateNodeObjectAsync(StandardMenuNodeEditModel model)
    {
        var contentItem = await contentManager.NewAsync(model.Type);
        contentItem.DisplayText = model.Text?.Trim() ?? string.Empty;
        ApplyNode(contentItem.Content, model);
        EnsureMenuItemsList(contentItem.Content);
        var node = JsonSerializer.SerializeToNode(contentItem)?.AsObject() ?? [];
        EnsureMenuItemsList(node);
        return node;
    }

    private static void ApplyNode(JsonObject node, StandardMenuNodeEditModel model)
    {
        node["DisplayText"] = model.Text?.Trim() ?? string.Empty;
        var type = ReadString(node, "ContentType") ?? model.Type;

        if (string.Equals(type, "HtmlMenuItem", StringComparison.OrdinalIgnoreCase))
        {
            node["HtmlMenuItemPart"] = new JsonObject
            {
                [nameof(HtmlMenuItemPart.Url)] = model.Url?.Trim() ?? string.Empty,
                [nameof(HtmlMenuItemPart.Target)] = model.Target?.Trim() ?? string.Empty,
                [nameof(HtmlMenuItemPart.Html)] = model.Html?.Trim() ?? string.Empty,
            };
        }
        else
        {
            node["LinkMenuItemPart"] = new JsonObject
            {
                [nameof(LinkMenuItemPart.Url)] = model.Url?.Trim() ?? string.Empty,
                [nameof(LinkMenuItemPart.Target)] = model.Target?.Trim() ?? string.Empty,
            };
        }

        if (model.PermissionNames is { Length: > 0 })
        {
            node["MenuItemPermissionPart"] = new JsonObject { ["PermissionNames"] = new JsonArray(model.PermissionNames.Select(name => JsonValue.Create(name)).ToArray()) };
        }
        else
        {
            node.Remove("MenuItemPermissionPart");
        }

        EnsureMenuItemsList(node);
    }

    private static bool TryGetChildren(JsonObject menuOrNode, string? parentNodeId, out JsonArray children)
    {
        if (string.IsNullOrWhiteSpace(parentNodeId))
        {
            children = EnsureMenuItemsList(menuOrNode);
            return true;
        }

        if (TryFindNode(menuOrNode, parentNodeId, out var parent, out _))
        {
            children = EnsureMenuItemsList(parent);
            return true;
        }

        children = [];
        return false;
    }

    private static bool TryFindNode(JsonObject source, string nodeId, out JsonObject node, out JsonArray? siblings)
    {
        foreach (var child in EnsureMenuItemsList(source).OfType<JsonObject>())
        {
            if (string.Equals(ReadString(child, "ContentItemId"), nodeId, StringComparison.OrdinalIgnoreCase))
            {
                node = child;
                siblings = EnsureMenuItemsList(source);
                return true;
            }

            if (TryFindNode(child, nodeId, out node, out siblings))
            {
                return true;
            }
        }

        node = [];
        siblings = null;
        return false;
    }

    private static bool IsDescendant(JsonObject node, string? possibleDescendantId)
    {
        if (string.IsNullOrWhiteSpace(possibleDescendantId))
        {
            return false;
        }

        foreach (var child in EnsureMenuItemsList(node).OfType<JsonObject>())
        {
            if (string.Equals(ReadString(child, "ContentItemId"), possibleDescendantId, StringComparison.OrdinalIgnoreCase) || IsDescendant(child, possibleDescendantId))
            {
                return true;
            }
        }

        return false;
    }

    private static JsonArray EnsureMenuItemsList(JsonObject source)
    {
        if (source["MenuItemsListPart"] is not JsonObject part)
        {
            source["MenuItemsListPart"] = part = [];
        }

        if (part["MenuItems"] is not JsonArray items)
        {
            part["MenuItems"] = items = [];
        }

        return items;
    }

    private static void RegenerateNodeIds(JsonObject source)
    {
        source["ContentItemId"] = Guid.NewGuid().ToString("n");
        source["ContentItemVersionId"] = Guid.NewGuid().ToString("n");
        foreach (var child in EnsureMenuItemsList(source).OfType<JsonObject>())
        {
            RegenerateNodeIds(child);
        }
    }

    private static JsonObject CloneObject(JsonObject source) => source.DeepClone().AsObject();

    private static int ClampPosition(int? position, int count) => Math.Clamp(position ?? count, 0, count);

    private static string? ReadString(JsonObject source, string propertyName) => source[propertyName]?.GetValue<string>();
}
