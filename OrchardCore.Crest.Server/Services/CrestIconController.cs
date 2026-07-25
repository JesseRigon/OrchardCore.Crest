using Crest.Controllers;

namespace Crest.Services;

/// <summary>
/// Resolves Crest icon declarations into response-level icon packs.
/// Explicit Crest/Iconify declarations are preferred; legacy Orchard and Font Awesome metadata
/// is normalized into Iconify keys so older modules still get icons in the headless UI path.
/// </summary>
public sealed class CrestIconController(CrestIconSourceStore iconSourceStore)
{
    public const string AdminMenuSearchIconKey = "iconify.mdi/current/default/magnify";

    public async Task<NavigationMenu> ResolveMenuIconsAsync(NavigationMenu menu, IEnumerable<string>? additionalIconKeys = null, CancellationToken cancellationToken = default)
    {
        var resolved = menu with
        {
            Items = await Task.WhenAll(menu.Items.Select(item => ResolveItemIconsAsync(item, cancellationToken)))
        };

        var iconKeys = CollectIconKeys(resolved.Items).Concat(additionalIconKeys ?? []);
        return resolved with
        {
            Icons = await iconSourceStore.BuildPackAsync(iconKeys, cancellationToken)
        };
    }

    private async Task<NavigationItem> ResolveItemIconsAsync(NavigationItem item, CancellationToken cancellationToken)
    {
        var icon = await ResolveIconAsync(item.Text, item.Id, item.Classes, cancellationToken);

        return item with
        {
            Icon = icon,
            Items = await Task.WhenAll(item.Items.Select(child => ResolveItemIconsAsync(child, cancellationToken)))
        };
    }

    private async Task<NavigationIcon?> ResolveIconAsync(string text, string? itemId, string[] classes, CancellationToken cancellationToken)
    {
        var iconClass = GetIconClass(classes);

        if (string.IsNullOrWhiteSpace(iconClass))
        {
            iconClass = await iconSourceStore.ResolveNavigationItemIconClassAsync(text, itemId, classes, cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(iconClass))
        {
            return null;
        }

        var resolved = await iconSourceStore.ResolveIconClassAsync(iconClass, cancellationToken);
        return resolved is null
            ? null
            : new NavigationIcon(resolved.Key, resolved.Library, resolved.Version, resolved.Style, resolved.Name, null);
    }

    private static IEnumerable<string> CollectIconKeys(IEnumerable<NavigationItem> items)
    {
        foreach (var item in items)
        {
            if (!string.IsNullOrWhiteSpace(item.Icon?.Key))
            {
                yield return item.Icon.Key;
            }

            foreach (var child in CollectIconKeys(item.Items))
            {
                yield return child;
            }
        }
    }

    private static string? GetIconClass(string[] classes)
    {
        var iconClasses = GetIconClasses(classes);

        return iconClasses.Length == 0 ? null : string.Join(" ", iconClasses);
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
