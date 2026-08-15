using System.Text.Json.Serialization;
using Crest.Controllers;
using OrchardCore.Data.Documents;
using OrchardCore.Documents;

namespace Crest.Services;

public sealed class CrestAdminMenuLayoutService(
    IDocumentManager<CrestAdminMenuLayoutDocument> documents,
    ICrestAdminMenuLayoutInvalidator invalidator)
{
    public const string DefaultMenuId = "__crest_default_admin_menu";
    public const string DefaultMenuName = "Sidebar";
    public const string LockedNewItemKey = "new";

    public async Task<CrestAdminMenuLayoutDocument> GetAsync() => await documents.GetOrCreateImmutableAsync();

    public async Task<CrestAdminMenuLayoutDocument> LoadAsync() => await documents.GetOrCreateMutableAsync();

    public Task SaveAsync(CrestAdminMenuLayoutDocument document) =>
        documents.UpdateAsync(document, _ => invalidator.InvalidateTenantAsync());

    public async Task<CrestAdminMenuLayoutFile> ExportAsync()
    {
        var layout = await GetAsync();
        return new CrestAdminMenuLayoutFile
        {
            Items = layout.Items.ToList(),
            CustomItems = layout.CustomItems.ToList(),
            Separators = layout.Separators.ToList(),
        };
    }

    public async Task ImportAsync(CrestAdminMenuLayoutFile file)
    {
        var layout = await LoadAsync();
        layout.Items = file.Items ?? [];
        layout.CustomItems = file.CustomItems ?? [];
        layout.Separators = file.Separators ?? [];
        await SaveAsync(layout);
    }

    public async Task<NavigationMenu> ApplyAsync(NavigationMenu menu)
    {
        var layout = await GetAsync();
        return menu with
        {
            Items = Apply(menu.Items, layout),
            Separators = GetSeparators(menu.Items, layout, includeHidden: false),
        };
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
        return ApplyToMenu(baseMenu, layout);
    }

    public async Task<NavigationMenu> ToggleAsync(NavigationMenu baseMenu, string itemKey)
    {
        var layout = await LoadAsync();
        var item = GetOrCreateOverride(layout, itemKey);
        item.Hidden = !item.Hidden;
        await SaveAsync(layout);
        return ApplyToMenu(baseMenu, layout);
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
        return ApplyToMenu(baseMenu, layout);
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

        return ApplyToMenu(baseMenu, layout);
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
            return ApplyToMenu(baseMenu, layout);
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
        return ApplyToMenu(baseMenu, layout);
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
            return ApplyToMenu(baseMenu, layout);
        }

        var item = GetOrCreateOverride(layout, key);
        var renamedText = text?.Trim();
        item.DisplayText = string.IsNullOrWhiteSpace(renamedText) || string.Equals(renamedText, node.Item.Text, StringComparison.Ordinal)
            ? null
            : renamedText;

        await SaveAsync(layout);
        return ApplyToMenu(baseMenu, layout);
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
        return ApplyToMenu(baseMenu, layout);
    }

    public async Task<NavigationMenu> CreateSeparatorAsync(NavigationMenu baseMenu, string? parentKey, int? position)
    {
        var layout = await LoadAsync();
        var flat = new Dictionary<string, LayoutNode>(StringComparer.Ordinal);
        Flatten(baseMenu.Items, null, flat);
        FlattenCustom(layout, flat);
        SnapshotKnownItems(layout, flat);

        parentKey = string.IsNullOrWhiteSpace(parentKey) ? null : parentKey;
        if (parentKey is not null && !flat.ContainsKey(parentKey))
        {
            parentKey = null;
        }

        var childCount = flat.Values.Count(node => string.Equals(GetEffectiveParent(layout, node), parentKey, StringComparison.Ordinal));
        var separatorCount = layout.Separators.Count(separator => string.Equals(separator.ParentKey, parentKey, StringComparison.Ordinal));
        var order = Math.Clamp(position ?? childCount + separatorCount, 0, childCount + separatorCount);

        ShiftSiblingOrders(layout, flat, parentKey, order);
        layout.Separators.Add(new CrestAdminMenuSeparator
        {
            Key = "separator-" + Guid.NewGuid().ToString("n"),
            ParentKey = parentKey,
            Order = order,
        });

        await SaveAsync(layout);
        return ApplyToMenu(baseMenu, layout);
    }

    public async Task<NavigationMenu> DeleteSeparatorAsync(NavigationMenu baseMenu, string key)
    {
        var layout = await LoadAsync();
        layout.Separators.RemoveAll(separator => string.Equals(separator.Key, key, StringComparison.Ordinal));
        await SaveAsync(layout);
        return ApplyToMenu(baseMenu, layout);
    }

    public async Task<NavigationMenu> MoveSeparatorAsync(NavigationMenu baseMenu, string key, string? parentKey, int? position)
    {
        var layout = await LoadAsync();
        var flat = new Dictionary<string, LayoutNode>(StringComparer.Ordinal);
        Flatten(baseMenu.Items, null, flat);
        FlattenCustom(layout, flat);
        SnapshotKnownItems(layout, flat);

        var separator = layout.Separators.FirstOrDefault(separator => string.Equals(separator.Key, key, StringComparison.Ordinal));
        if (separator is null)
        {
            return ApplyToMenu(baseMenu, layout);
        }

        parentKey = string.IsNullOrWhiteSpace(parentKey) ? null : parentKey;
        if (parentKey is not null && (!flat.ContainsKey(parentKey) || IsDescendant(flat, layout, parentKey, key)))
        {
            parentKey = null;
        }

        var maxPosition = flat.Values.Count(node => string.Equals(GetEffectiveParent(layout, node), parentKey, StringComparison.Ordinal))
            + layout.Separators.Count(candidate => !string.Equals(candidate.Key, key, StringComparison.Ordinal) && string.Equals(candidate.ParentKey, parentKey, StringComparison.Ordinal));
        var order = Math.Clamp(position ?? maxPosition, 0, maxPosition);

        ShiftSiblingOrders(layout, flat, parentKey, order, excludeSeparatorKey: key);
        separator.ParentKey = parentKey;
        separator.Order = order;

        await SaveAsync(layout);
        return ApplyToMenu(baseMenu, layout);
    }

    public async Task<bool> IsCustomAsync(string key)
    {
        var layout = await GetAsync();
        return layout.CustomItems.Any(item => string.Equals(item.Key, key, StringComparison.Ordinal));
    }

    // Removes override rows that no longer correspond to anything in the CURRENT menu
    // tree - e.g. rows left behind by the fixed Href-based Key bug (NavigationItem.Key
    // used to be a per-request-generated route Href for some items, which could differ
    // between the request that saved a drag-drop move and the request that re-applied
    // it, permanently orphaning the old row under a key nothing will ever match again).
    // Only safe to call when baseMenu reflects the tenant's full feature set (all
    // features enabled) - otherwise a row for a merely-DISABLED-but-installed feature's
    // item would look orphaned too and get wrongly deleted. Custom items are always
    // user-authored and kept regardless of tree membership.
    public async Task<int> PruneOrphanedOverridesAsync(NavigationMenu baseMenu)
    {
        var flat = new Dictionary<string, LayoutNode>(StringComparer.Ordinal);
        Flatten(baseMenu.Items, null, flat);

        var layout = await LoadAsync();
        var customKeys = new HashSet<string>(layout.CustomItems.Select(item => item.Key), StringComparer.Ordinal);
        var live = new HashSet<string>(flat.Keys, StringComparer.Ordinal);
        live.UnionWith(customKeys);
        live.Add(LockedNewItemKey);

        var orphanedKeys = layout.Items
            .Select(item => item.ItemKey)
            .Where(key => !live.Contains(key))
            .ToHashSet(StringComparer.Ordinal);

        if (orphanedKeys.Count == 0)
        {
            return 0;
        }

        layout.Items.RemoveAll(item => orphanedKeys.Contains(item.ItemKey));

        foreach (var item in layout.Items.Where(item => item.ParentKey is not null && orphanedKeys.Contains(item.ParentKey)))
        {
            item.ParentKey = null;
        }

        foreach (var separator in layout.Separators.Where(separator => separator.ParentKey is not null && orphanedKeys.Contains(separator.ParentKey)))
        {
            separator.ParentKey = null;
        }

        await SaveAsync(layout);
        return orphanedKeys.Count;
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

    private NavigationMenu ApplyToMenu(NavigationMenu baseMenu, CrestAdminMenuLayoutDocument layout) => baseMenu with
    {
        Items = Apply(baseMenu.Items, layout),
        Separators = GetSeparators(baseMenu.Items, layout, includeHidden: false),
    };

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

    // Items without an Id have no stable identity to persist layout/icon overrides
    // against and are skipped entirely, along with their descendants.
    private static void Flatten(IEnumerable<NavigationItem> items, string? parentKey, Dictionary<string, LayoutNode> flat)
    {
        var index = 0;
        foreach (var item in items)
        {
            var key = item.Key;
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

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

    private static NavigationSeparator[] GetSeparators(NavigationItem[] items, CrestAdminMenuLayoutDocument layout, bool includeHidden)
    {
        var flat = new Dictionary<string, LayoutNode>(StringComparer.Ordinal);
        Flatten(items, null, flat);
        FlattenCustom(layout, flat);
        var visible = flat.Values
            .Where(node => includeHidden || !IsHiddenWithAncestor(flat, layout, node))
            .ToDictionary(node => node.Key, StringComparer.Ordinal);

        return layout.Separators
            .Where(separator => !string.IsNullOrWhiteSpace(separator.Key))
            .Where(separator => string.IsNullOrWhiteSpace(separator.ParentKey) || visible.ContainsKey(separator.ParentKey))
            .Select(separator => new NavigationSeparator(separator.Key, string.IsNullOrWhiteSpace(separator.ParentKey) ? null : separator.ParentKey, separator.Order))
            .ToArray();
    }

    private static void ShiftSiblingOrders(CrestAdminMenuLayoutDocument layout, Dictionary<string, LayoutNode> flat, string? parentKey, int fromOrder, string? excludeSeparatorKey = null)
    {
        foreach (var node in flat.Values.Where(node => string.Equals(GetEffectiveParent(layout, node), parentKey, StringComparison.Ordinal)))
        {
            var item = GetOrCreateOverride(layout, node.Key);
            var order = item.Order ?? node.BaseOrder;
            if (order >= fromOrder)
            {
                item.Order = order + 1;
            }
        }

        foreach (var separator in layout.Separators.Where(separator => !string.Equals(separator.Key, excludeSeparatorKey, StringComparison.Ordinal) && string.Equals(separator.ParentKey, parentKey, StringComparison.Ordinal) && separator.Order >= fromOrder))
        {
            separator.Order++;
        }
    }

    private static string[] ToIconClasses(string iconClass) =>
        iconClass.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select((value, index) => index == 0 ? "icon-class-" + value : value)
            .ToArray();

    // Materializes an override row for every item currently in the tree, so that a
    // subsequent reorder has a row to write Order into for each sibling. Deliberately
    // records NOTHING but the Id: the layout document is identity-only, and storing the
    // caption here (as this used to) wrote whatever culture the editing request ran under
    // into the tenant's layout, which then leaked into exported recipes as translated
    // strings.
    private static void SnapshotKnownItems(CrestAdminMenuLayoutDocument layout, Dictionary<string, LayoutNode> flat)
    {
        foreach (var node in flat.Values)
        {
            GetOrCreateOverride(layout, node.Key);
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
    public List<CrestAdminMenuSeparator> Separators { get; set; } = [];
}

public sealed class CrestAdminMenuLayoutFile
{
    public List<CrestAdminMenuLayoutItem> Items { get; set; } = [];
    public List<CrestAdminMenuCustomItem> CustomItems { get; set; } = [];
    public List<CrestAdminMenuSeparator> Separators { get; set; } = [];
}

// Available options are expected to grow (more anchor corners, responsive-size-specific
// choices) — keep this an open enum rather than a bool.
[JsonConverter(typeof(JsonStringEnumConverter<PrimaryNavMenuCollapseIconPosition>))]
public enum PrimaryNavMenuCollapseIconPosition
{
    OutsideBottomRight,
    InsideBottomLeft,
}

public sealed class CrestPrimaryNavMenuSettings
{
    public bool Collapsible { get; set; } = true;
    public int ExpansionDurationMilliseconds { get; set; } = 500;
    public List<bool> TierSeparators { get; set; } = [true, false, false];
    public List<string> TierIndents { get; set; } = ["0rem", "0.75rem", "1.25rem", "1.75rem"];
    public List<string> TierBackgrounds { get; set; } = ["transparent", "transparent", "var(--rz-base-100, color-mix(in srgb, var(--rz-base-background-color) 88%, var(--rz-text-color) 12%))", "transparent"];
    public List<string> TierBaseSizes { get; set; } = ["1rem", "0.95rem", "0.9rem"];
    public List<double> TierBaseRems { get; set; } = [1.0, 0.95, 0.9];
    public PrimaryNavMenuCollapseIconPosition CollapseIconPosition { get; set; } = PrimaryNavMenuCollapseIconPosition.OutsideBottomRight;

    public static CrestPrimaryNavMenuSettings Default => new();

    public static CrestPrimaryNavMenuSettings Normalize(CrestPrimaryNavMenuSettings? settings)
    {
        var source = settings ?? Default;
        var normalized = new CrestPrimaryNavMenuSettings
        {
            Collapsible = source.Collapsible,
            ExpansionDurationMilliseconds = source.ExpansionDurationMilliseconds,
            TierSeparators = source.TierSeparators?.ToList() ?? [],
            TierIndents = source.TierIndents?.ToList() ?? [],
            TierBackgrounds = source.TierBackgrounds?.ToList() ?? [],
            TierBaseSizes = source.TierBaseSizes?.ToList() ?? [],
            TierBaseRems = source.TierBaseRems?.ToList() ?? [],
            CollapseIconPosition = Enum.IsDefined(source.CollapseIconPosition) ? source.CollapseIconPosition : Default.CollapseIconPosition,
        };

        normalized.ExpansionDurationMilliseconds = Math.Clamp(normalized.ExpansionDurationMilliseconds, 100, 2000);
        normalized.TierSeparators = NormalizeList(normalized.TierSeparators, Default.TierSeparators, 3);
        normalized.TierIndents = NormalizeList(normalized.TierIndents, Default.TierIndents, 4);
        normalized.TierBackgrounds = NormalizeList(normalized.TierBackgrounds, Default.TierBackgrounds, 4);
        normalized.TierBaseSizes = NormalizeList(
            normalized.TierBaseSizes is { Count: > 0 } ? normalized.TierBaseSizes : normalized.TierBaseRems.Select(value => $"{value:0.###}rem"),
            Default.TierBaseSizes,
            3);
        normalized.TierBaseRems = NormalizeList(normalized.TierBaseRems, Default.TierBaseRems, 3)
            .Select(value => Math.Clamp(value, 0.5, 2.0))
            .ToList();
        return normalized;
    }

    private static List<T> NormalizeList<T>(IEnumerable<T>? source, IReadOnlyList<T> defaults, int length)
    {
        var values = source?.ToList() ?? [];
        while (values.Count < length)
        {
            values.Add(defaults[Math.Min(values.Count, defaults.Count - 1)]);
        }

        return values.Take(length).ToList();
    }
}

// An override row for a stock Orchard menu item, addressed purely by its stable
// MenuItem.Id (ItemKey/ParentKey). Everything here is either identity or a deliberate
// user authoring decision - notably there is NO copy of the item's own caption: that is
// Orchard's, resolved per-request per-culture, and snapshotting it here made a tenant's
// layout (and its exported recipe) carry whatever language the admin happened to be
// using when they last dragged something. DisplayText is the one caption in this type
// and it is a genuine user-authored rename, not a snapshot.
public sealed class CrestAdminMenuLayoutItem
{
    public string ItemKey { get; set; } = string.Empty;
    public string? ParentKey { get; set; }
    public int? Order { get; set; }
    public bool Hidden { get; set; }
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

public sealed class CrestAdminMenuSeparator
{
    public string Key { get; set; } = string.Empty;
    public string? ParentKey { get; set; }
    public int Order { get; set; }
}
