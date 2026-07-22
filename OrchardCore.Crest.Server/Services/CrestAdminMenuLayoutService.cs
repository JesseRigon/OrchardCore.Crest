using Crest.Controllers;
using OrchardCore.Data.Documents;
using OrchardCore.Documents;

namespace Crest.Services;

public sealed class CrestAdminMenuLayoutService(IDocumentManager<CrestAdminMenuLayoutDocument> documents)
{
    public const string DefaultMenuId = "__crest_default_admin_menu";
    public const string DefaultMenuName = "Sidebar Layout";
    public const string LockedNewItemKey = "new";

    public async Task<CrestAdminMenuLayoutDocument> GetAsync() => await documents.GetOrCreateImmutableAsync();

    public async Task<CrestAdminMenuLayoutDocument> LoadAsync() => await documents.GetOrCreateMutableAsync();

    public Task SaveAsync(CrestAdminMenuLayoutDocument document) => documents.UpdateAsync(document);

    public async Task<CrestAdminMenuLayoutFile> ExportAsync()
    {
        var layout = await GetAsync();
        return new CrestAdminMenuLayoutFile
        {
            Items = layout.Items.ToList(),
            CustomItems = layout.CustomItems.ToList(),
        };
    }

    public async Task ImportAsync(CrestAdminMenuLayoutFile file)
    {
        var layout = await LoadAsync();
        layout.Items = file.Items ?? [];
        layout.CustomItems = file.CustomItems ?? [];
        await SaveAsync(layout);
    }

    public async Task<NavigationMenu> ApplyAsync(NavigationMenu menu)
    {
        var layout = await GetAsync();
        return menu with { Items = Apply(menu.Items, layout) };
    }

    public NavigationItem[] Apply(NavigationItem[] items, CrestAdminMenuLayoutDocument layout) => Apply(items, layout, includeHidden: false);

    public NavigationItem[] ApplyForManagement(NavigationItem[] items, CrestAdminMenuLayoutDocument layout) => Apply(items, layout, includeHidden: true);

    public bool IsHidden(CrestAdminMenuLayoutDocument layout, string key) => GetOverride(layout, key).Hidden;

    private NavigationItem[] Apply(NavigationItem[] items, CrestAdminMenuLayoutDocument layout, bool includeHidden)
    {
        var flat = new Dictionary<string, LayoutNode>(StringComparer.Ordinal);
        Flatten(items, null, flat);
        FlattenCustom(layout, flat);

        var visible = flat.Values
            .Where(node => includeHidden || !IsHiddenWithAncestor(flat, layout, node))
            .ToDictionary(node => node.Key, StringComparer.Ordinal);

        var childMap = visible.Keys.ToDictionary(key => key, _ => new List<LayoutNode>(), StringComparer.Ordinal);
        var roots = new List<LayoutNode>();

        foreach (var node in visible.Values.ToArray())
        {
            var itemOverride = GetOverride(layout, node.Key);
            var parentKey = !string.IsNullOrWhiteSpace(itemOverride.ParentKey) ? itemOverride.ParentKey : node.BaseParentKey;
            if (parentKey is null || IsDescendant(flat, layout, node.Key, parentKey))
            {
                roots.Add(node);
                continue;
            }

            if (visible.ContainsKey(parentKey))
            {
                childMap[parentKey].Add(node);
                continue;
            }

            // The input tree is already authorization-filtered by Orchard's
            // INavigationManager. A saved layout must never recreate a parent
            // that Orchard omitted for this request. Keep an authorized child
            // reachable if its saved parent is unavailable, but do not expose
            // that absent parent as a synthetic menu node.
            roots.Add(node);
        }

        return Build(roots, childMap, layout);
    }

    public async Task<NavigationMenu> MoveAsync(NavigationMenu baseMenu, string itemKey, string? parentKey, int? position)
    {
        var layout = await LoadAsync();
        var flat = new Dictionary<string, LayoutNode>(StringComparer.Ordinal);
        Flatten(baseMenu.Items, null, flat);
        FlattenCustom(layout, flat);

        SnapshotKnownItems(layout, flat);

        if (!flat.TryGetValue(itemKey, out var node) || itemKey == parentKey || IsDescendant(flat, layout, itemKey, parentKey))
        {
            return await ApplyAsync(baseMenu);
        }

        if (!string.IsNullOrWhiteSpace(parentKey) && !flat.ContainsKey(parentKey))
        {
            parentKey = null;
        }

        var siblings = flat.Values
            .Where(candidate => candidate.Key != itemKey && !GetOverride(layout, candidate.Key).Hidden)
            .Where(candidate => string.Equals(GetEffectiveParent(layout, candidate), parentKey, StringComparison.Ordinal))
            .OrderBy(candidate => GetOverride(layout, candidate.Key).Order ?? candidate.BaseOrder)
            .Select(candidate => candidate.Key)
            .ToList();

        var index = Math.Clamp(position ?? siblings.Count, 0, siblings.Count);
        siblings.Insert(index, itemKey);

        foreach (var siblingKey in siblings)
        {
            var item = GetOrCreateOverride(layout, siblingKey);
            item.ParentKey = parentKey;
            item.Order = siblings.IndexOf(siblingKey);
        }

        var moved = GetOrCreateOverride(layout, itemKey);
        moved.ParentKey = parentKey;
        moved.Order = index;

        await SaveAsync(layout);
        return baseMenu with { Items = Apply(baseMenu.Items, layout) };
    }

    public async Task<NavigationMenu> ToggleAsync(NavigationMenu baseMenu, string itemKey)
    {
        var layout = await LoadAsync();
        var item = GetOrCreateOverride(layout, itemKey);
        item.Hidden = !item.Hidden;
        await SaveAsync(layout);
        return baseMenu with { Items = Apply(baseMenu.Items, layout) };
    }

    public async Task<NavigationMenu> CreateCustomAsync(NavigationMenu baseMenu, string text, string? url, string? iconClass, string? parentKey, int? position)
    {
        var layout = await LoadAsync();
        var key = "custom-" + Guid.NewGuid().ToString("n");
        layout.CustomItems.Add(new CrestAdminMenuCustomItem
        {
            Key = key,
            Text = text,
            Url = url,
            IconClass = iconClass,
        });

        var item = GetOrCreateOverride(layout, key);
        item.ParentKey = string.IsNullOrWhiteSpace(parentKey) ? null : parentKey;
        item.Order = position;
        await SaveAsync(layout);
        return baseMenu with { Items = Apply(baseMenu.Items, layout) };
    }

    public async Task<NavigationMenu> UpdateCustomAsync(NavigationMenu baseMenu, string key, string text, string? url, string? iconClass)
    {
        var layout = await LoadAsync();
        var item = layout.CustomItems.FirstOrDefault(item => string.Equals(item.Key, key, StringComparison.Ordinal));
        if (item is not null)
        {
            item.Text = text;
            item.Url = url;
            item.IconClass = iconClass;
            await SaveAsync(layout);
        }

        return baseMenu with { Items = Apply(baseMenu.Items, layout) };
    }

    public async Task<NavigationMenu> UpdateItemAsync(NavigationMenu baseMenu, string key, string? text, string? iconClass, string? parentKey, int? position)
    {
        var layout = await LoadAsync();
        var flat = new Dictionary<string, LayoutNode>(StringComparer.Ordinal);
        Flatten(baseMenu.Items, null, flat);
        FlattenCustom(layout, flat);
        SnapshotKnownItems(layout, flat);

        if (!flat.TryGetValue(key, out var node) || key == parentKey || IsDescendant(flat, layout, key, parentKey))
        {
            return baseMenu with { Items = Apply(baseMenu.Items, layout) };
        }

        var item = GetOrCreateOverride(layout, key);
        var displayText = text?.Trim();
        item.DisplayText = string.IsNullOrWhiteSpace(displayText) || string.Equals(displayText, node.Item.Text, StringComparison.Ordinal)
            ? null
            : displayText;
        item.IconClass = string.IsNullOrWhiteSpace(iconClass) ? null : iconClass.Trim();

        if (parentKey is not null || position.HasValue)
        {
            if (!string.IsNullOrWhiteSpace(parentKey) && !flat.ContainsKey(parentKey))
            {
                parentKey = null;
            }

            item.ParentKey = parentKey;
            item.Order = position;
        }

        await SaveAsync(layout);
        return baseMenu with { Items = Apply(baseMenu.Items, layout) };
    }

    public async Task<NavigationMenu> RenameAsync(NavigationMenu baseMenu, string key, string? text)
    {
        var layout = await LoadAsync();
        var flat = new Dictionary<string, LayoutNode>(StringComparer.Ordinal);
        Flatten(baseMenu.Items, null, flat);
        FlattenCustom(layout, flat);
        SnapshotKnownItems(layout, flat);

        if (!flat.TryGetValue(key, out var node))
        {
            return baseMenu with { Items = Apply(baseMenu.Items, layout) };
        }

        var item = GetOrCreateOverride(layout, key);
        var renamedText = text?.Trim();
        item.DisplayText = string.IsNullOrWhiteSpace(renamedText) || string.Equals(renamedText, node.Item.Text, StringComparison.Ordinal)
            ? null
            : renamedText;

        await SaveAsync(layout);
        return baseMenu with { Items = Apply(baseMenu.Items, layout) };
    }

    public async Task<NavigationMenu> DeleteCustomAsync(NavigationMenu baseMenu, string key)
    {
        var layout = await LoadAsync();
        layout.CustomItems.RemoveAll(item => string.Equals(item.Key, key, StringComparison.Ordinal));
        layout.Items.RemoveAll(item => string.Equals(item.ItemKey, key, StringComparison.Ordinal));
        foreach (var item in layout.Items.Where(item => string.Equals(item.ParentKey, key, StringComparison.Ordinal)))
        {
            item.ParentKey = null;
        }

        await SaveAsync(layout);
        return baseMenu with { Items = Apply(baseMenu.Items, layout) };
    }

    public async Task<bool> IsCustomAsync(string key)
    {
        var layout = await GetAsync();
        return layout.CustomItems.Any(item => string.Equals(item.Key, key, StringComparison.Ordinal));
    }

    public async Task<bool> IsLockedNewBranchAsync(NavigationMenu baseMenu, string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        var layout = await GetAsync();
        var flat = new Dictionary<string, LayoutNode>(StringComparer.Ordinal);
        Flatten(baseMenu.Items, null, flat);
        FlattenCustom(layout, flat);

        return IsLockedNewBranch(flat, layout, key);
    }

    private static string? GetEffectiveParent(CrestAdminMenuLayoutDocument layout, LayoutNode node)
    {
        var item = GetOverride(layout, node.Key);
        return !string.IsNullOrWhiteSpace(item.ParentKey) ? item.ParentKey : node.BaseParentKey;
    }

    private static NavigationItem[] Build(List<LayoutNode> nodes, Dictionary<string, List<LayoutNode>> childMap, CrestAdminMenuLayoutDocument layout) => nodes
        .OrderBy(node => GetOverride(layout, node.Key).Order ?? node.BaseOrder)
        .Select(node =>
        {
            var itemOverride = GetOverride(layout, node.Key);
            var text = string.IsNullOrWhiteSpace(itemOverride.DisplayText) ? node.Item.Text : itemOverride.DisplayText;
            var classes = string.IsNullOrWhiteSpace(itemOverride.IconClass) ? node.Item.Classes : ToIconClasses(itemOverride.IconClass);
            return node.Item with { Text = text, Classes = classes, Items = Build(childMap[node.Key], childMap, layout) };
        })
        .ToArray();

    private static void Flatten(IEnumerable<NavigationItem> items, string? parentKey, Dictionary<string, LayoutNode> flat)
    {
        var index = 0;
        foreach (var item in items)
        {
            var key = item.Key;
            if (!flat.ContainsKey(key))
            {
                flat[key] = new LayoutNode(key, item with { Items = [] }, parentKey, index);
                Flatten(item.Items, key, flat);
            }

            index++;
        }
    }

    private static bool IsDescendant(Dictionary<string, LayoutNode> flat, CrestAdminMenuLayoutDocument layout, string itemKey, string? candidateParentKey)
    {
        while (!string.IsNullOrWhiteSpace(candidateParentKey) && flat.TryGetValue(candidateParentKey, out var parent))
        {
            if (parent.Key == itemKey)
            {
                return true;
            }

            candidateParentKey = GetEffectiveParent(layout, parent);
        }

        return false;
    }

    private static bool IsLockedNewBranch(Dictionary<string, LayoutNode> flat, CrestAdminMenuLayoutDocument layout, string key)
    {
        if (string.Equals(key, LockedNewItemKey, StringComparison.Ordinal))
        {
            return true;
        }

        if (!flat.TryGetValue(key, out var node))
        {
            return false;
        }

        var parentKey = GetEffectiveParent(layout, node);
        while (!string.IsNullOrWhiteSpace(parentKey) && flat.TryGetValue(parentKey, out var parent))
        {
            if (string.Equals(parent.Key, LockedNewItemKey, StringComparison.Ordinal))
            {
                return true;
            }

            parentKey = GetEffectiveParent(layout, parent);
        }

        return false;
    }

    private static string[] ToIconClasses(string iconClass) =>
        iconClass.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select((value, index) => index == 0 ? "icon-class-" + value : value)
            .ToArray();

    private static void SnapshotKnownItems(CrestAdminMenuLayoutDocument layout, Dictionary<string, LayoutNode> flat)
    {
        foreach (var node in flat.Values)
        {
            var item = GetOrCreateOverride(layout, node.Key);
            item.Text = node.Item.Text;
        }
    }

    private static void FlattenCustom(CrestAdminMenuLayoutDocument layout, Dictionary<string, LayoutNode> flat)
    {
        var index = 100000;
        foreach (var custom in layout.CustomItems)
        {
            if (string.IsNullOrWhiteSpace(custom.Key) || flat.ContainsKey(custom.Key))
            {
                continue;
            }

            var classes = string.IsNullOrWhiteSpace(custom.IconClass)
                ? []
                : custom.IconClass.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(value => "icon-class-" + value)
                    .ToArray();
            var item = new NavigationItem(custom.Text, custom.Key, null, custom.Url, null, null, null, classes, []);
            flat[custom.Key] = new LayoutNode(custom.Key, item, null, index++);
        }
    }

    private static bool IsHiddenWithAncestor(Dictionary<string, LayoutNode> flat, CrestAdminMenuLayoutDocument layout, LayoutNode node)
    {
        if (GetOverride(layout, node.Key).Hidden)
        {
            return true;
        }

        var parentKey = GetEffectiveParent(layout, node);
        while (!string.IsNullOrWhiteSpace(parentKey) && flat.TryGetValue(parentKey, out var parent))
        {
            if (GetOverride(layout, parent.Key).Hidden)
            {
                return true;
            }

            parentKey = GetEffectiveParent(layout, parent);
        }

        return false;
    }

    private static CrestAdminMenuLayoutItem GetOverride(CrestAdminMenuLayoutDocument layout, string key)
        => layout.Items.FirstOrDefault(item => string.Equals(item.ItemKey, key, StringComparison.Ordinal)) ?? new CrestAdminMenuLayoutItem { ItemKey = key };

    private static CrestAdminMenuLayoutItem GetOrCreateOverride(CrestAdminMenuLayoutDocument layout, string key)
    {
        var item = layout.Items.FirstOrDefault(item => string.Equals(item.ItemKey, key, StringComparison.Ordinal));
        if (item is not null)
        {
            return item;
        }

        item = new CrestAdminMenuLayoutItem { ItemKey = key };
        layout.Items.Add(item);
        return item;
    }

    private sealed record LayoutNode(string Key, NavigationItem Item, string? BaseParentKey, int BaseOrder);
}

public sealed class CrestAdminMenuLayoutDocument : Document
{
    public List<CrestAdminMenuLayoutItem> Items { get; set; } = [];
    public List<CrestAdminMenuCustomItem> CustomItems { get; set; } = [];
}

public sealed class CrestAdminMenuLayoutFile
{
    public List<CrestAdminMenuLayoutItem> Items { get; set; } = [];
    public List<CrestAdminMenuCustomItem> CustomItems { get; set; } = [];
}

public sealed class CrestAdminMenuLayoutItem
{
    public string ItemKey { get; set; } = string.Empty;
    public string? ParentKey { get; set; }
    public int? Order { get; set; }
    public bool Hidden { get; set; }
    public string? Text { get; set; }
    public string? DisplayText { get; set; }
    public string? IconClass { get; set; }
}

public sealed class CrestAdminMenuCustomItem
{
    public string Key { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public string? Url { get; set; }
    public string? IconClass { get; set; }
}
