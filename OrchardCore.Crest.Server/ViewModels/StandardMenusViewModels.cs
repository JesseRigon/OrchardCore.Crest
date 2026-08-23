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

namespace Crest.ViewModels;

public sealed record StandardMenusState(StandardMenuSummary[] Menus, StandardMenuNodeType[] AvailableNodeTypes);

public sealed record StandardMenuNodeType(string Type, string DisplayName);

public sealed record StandardMenuEditModel(string? Name, bool Published);

public sealed record StandardMenuNodeEditModel(
    string Type,
    string Text,
    string? Url,
    string? Target,
    string? Html,
    string[]? PermissionNames,
    string? ParentNodeId,
    int? Position);

public sealed record StandardMenuNodeMoveModel(string? ParentNodeId, int? Position);

public sealed record StandardMenuSummary(
    string Id,
    string ContentItemId,
    string ContentItemVersionId,
    string Name,
    bool Published,
    StandardMenuNodeSummary[] Nodes)
{
    public static StandardMenuSummary From(OrchardContentItem menu) => new(
        menu.ContentItemId,
        menu.ContentItemId,
        menu.ContentItemVersionId,
        string.IsNullOrWhiteSpace(menu.DisplayText) ? menu.ContentItemId : menu.DisplayText,
        menu.Published,
        ReadChildren((JsonObject)menu.Content).Select((node, index) => StandardMenuNodeSummary.From(node, null, 0, index)).ToArray());

    internal static IEnumerable<JsonObject> ReadChildren(JsonObject source)
    {
        if (source["MenuItemsListPart"] is not JsonObject part || part["MenuItems"] is not JsonArray items)
        {
            yield break;
        }

        foreach (var item in items.OfType<JsonObject>())
        {
            yield return item;
        }
    }
}

public sealed record StandardMenuNodeSummary(
    string Id,
    string Type,
    string Text,
    string? Url,
    string? Target,
    string? Html,
    bool Enabled,
    int Depth,
    int Order,
    string? ParentId,
    string[] PermissionNames,
    StandardMenuNodeSummary[] Items)
{
    public static StandardMenuNodeSummary From(JsonObject item, string? parentId, int depth, int order)
    {
        var id = ReadString(item, "ContentItemId") ?? $"menu-node-{depth}-{order}";
        var type = ReadString(item, "ContentType") ?? "MenuItem";
        var text = ReadString(item, "DisplayText") ?? type;
        var url = ReadString(item, "LinkMenuItemPart", "Url") ?? ReadString(item, "HtmlMenuItemPart", "Url");
        var target = ReadString(item, "LinkMenuItemPart", "Target") ?? ReadString(item, "HtmlMenuItemPart", "Target");
        var html = ReadString(item, "HtmlMenuItemPart", "Html");
        var permissions = ReadStringArray(item, "MenuItemPermissionPart", "PermissionNames");
        var children = StandardMenuSummary.ReadChildren(item)
            .Select((child, index) => From(child, id, depth + 1, index))
            .ToArray();

        return new(id, type, text, url, target, html, true, depth, order, parentId, permissions, children);
    }

    private static string? ReadString(JsonObject source, string propertyName) =>
        source[propertyName]?.GetValue<string>();

    private static string? ReadString(JsonObject source, string partName, string propertyName) =>
        source[partName] is JsonObject part ? part[propertyName]?.GetValue<string>() : null;

    private static string[] ReadStringArray(JsonObject source, string partName, string propertyName)
    {
        if (source[partName] is not JsonObject part || part[propertyName] is not JsonArray values)
        {
            return [];
        }

        return values.Select(value => value?.GetValue<string>())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .ToArray();
    }
}
