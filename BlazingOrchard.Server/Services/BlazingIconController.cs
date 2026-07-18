using BlazingOrchard.Controllers;

namespace BlazingOrchard.Services;

/// <summary>
/// Resolves raw menu icon declarations into SVG payloads.
/// This is the icon boundary: navigation/layout code passes raw classes through,
/// and this controller performs parsing + dictionary lookup without cross-library mapping.
/// </summary>
public sealed class BlazingIconController(BlazingIconSourceStore iconSourceStore)
{
    public async Task<NavigationMenu> ResolveMenuIconsAsync(NavigationMenu menu, CancellationToken cancellationToken = default) => menu with
    {
        Items = await Task.WhenAll(menu.Items.Select(item => ResolveItemIconsAsync(item, cancellationToken)))
    };

    private async Task<NavigationItem> ResolveItemIconsAsync(NavigationItem item, CancellationToken cancellationToken)
    {
        var icon = await ResolveIconAsync(item.Id, item.Classes, cancellationToken);

        return item with
        {
            Icon = icon,
            Items = await Task.WhenAll(item.Items.Select(child => ResolveItemIconsAsync(child, cancellationToken)))
        };
    }

    private async Task<NavigationIcon?> ResolveIconAsync(string? itemId, string[] classes, CancellationToken cancellationToken)
    {
        var iconClass = GetIconClass(classes);

        if (string.IsNullOrWhiteSpace(iconClass))
        {
            iconClass = await iconSourceStore.ResolveNavigationItemIconClassAsync(itemId, cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(iconClass))
        {
            return null;
        }

        var resolved = await iconSourceStore.ResolveIconClassAsync(iconClass, cancellationToken);
        return resolved is not null
            ? new NavigationIcon(resolved.Library, resolved.Version, resolved.Name, resolved.SvgMarkup)
            : new NavigationIcon("missing", null, iconClass, null);
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
