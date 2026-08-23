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

namespace Crest.ViewModels;

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
