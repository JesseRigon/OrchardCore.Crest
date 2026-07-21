using Crest.Icons;

namespace Crest.Services;

public sealed record CrestResolvedIcon(string Key, string Library, string? Version, string Style, string Name, string? SvgMarkup);

public sealed record CrestIconLibrary(string Id, string Name, string? Version, string ProviderId, string ProviderName);

public sealed record CrestIconSearchFilter(string Facet, string Value);

public sealed record CrestIconSearchFacet(string Id, string Label, string SelectionMode, CrestIconSearchFacetOption[] Options);

public sealed record CrestIconSearchFacetOption(string Value, string Label, int? Count);

public sealed record CrestIconCatalogItem(string Key, string Library, string? Version, string Style, string Name, string IconClass, string? SvgMarkup, string ProviderId);

public sealed record CrestIconSearchResult(CrestIconLibrary[] Libraries, CrestIconSearchFacet[] Facets, CrestIconCatalogItem[] Items, int Total, int Skip, int Take);

public sealed class CrestIconSourceStore(IIconRegistry iconRegistry)
{
    public async ValueTask<CrestIconSearchResult> SearchAsync(string? library, string? query, int skip, int take, CrestIconSearchFilter[]? filters = null, CancellationToken cancellationToken = default)
    {
        var result = await iconRegistry.SearchAsync(new IconSearchRequest(
            library,
            query,
            skip,
            take,
            filters?.Select(filter => new IconSearchFilter(filter.Facet, filter.Value)).ToArray()), cancellationToken);
        return new CrestIconSearchResult(
            result.Libraries.Select(library => new CrestIconLibrary(library.Id, library.Name, library.Version, library.ProviderId, library.ProviderName)).ToArray(),
            result.Facets.Select(facet => new CrestIconSearchFacet(
                facet.Id,
                facet.Label,
                facet.SelectionMode,
                facet.Options.Select(option => new CrestIconSearchFacetOption(option.Value, option.Label, option.Count)).ToArray())).ToArray(),
            result.Items.Select(item => new CrestIconCatalogItem(item.Key, item.Library, item.Version, item.Style, item.Name, item.IconClass, item.SvgMarkup, item.ProviderId)).ToArray(),
            result.Total,
            result.Skip,
            result.Take);
    }

    public ValueTask<string?> ResolveNavigationItemIconClassAsync(string? text, string? itemId, string[] classes, CancellationToken cancellationToken = default)
    {
        foreach (var value in GetLookupValues(text, itemId, classes))
        {
            if (LegacyNavigationIconMap.TryGetValue(value, out var iconClass))
            {
                return ValueTask.FromResult<string?>(iconClass);
            }
        }

        return ValueTask.FromResult<string?>(null);
    }

    public async ValueTask<CrestResolvedIcon?> ResolveIconClassAsync(string iconClass, CancellationToken cancellationToken = default)
    {
        var icon = await iconRegistry.ResolveDeclarationAsync(iconClass, cancellationToken);
        return icon is null
            ? null
            : new CrestResolvedIcon(icon.Key.ToString(), icon.Key.Library, icon.Key.Version, icon.Key.Style, icon.Key.Name, icon.SvgMarkup);
    }

    public async ValueTask<IconPack> BuildPackAsync(IEnumerable<string> iconKeys, CancellationToken cancellationToken = default)
    {
        var keys = iconKeys
            .Select(value => IconKey.TryParse(value, out var key) ? key : (IconKey?)null)
            .Where(key => key.HasValue)
            .Select(key => key!.Value);

        return await iconRegistry.BuildPackAsync(keys, cancellationToken);
    }

    private static IEnumerable<string> GetLookupValues(string? text, string? itemId, IEnumerable<string> classes)
    {
        if (!string.IsNullOrWhiteSpace(itemId))
        {
            yield return itemId;
        }

        foreach (var className in classes.SelectMany(value => value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)))
        {
            yield return className;
        }

        if (!string.IsNullOrWhiteSpace(text))
        {
            yield return text;
        }
    }

    private static readonly IReadOnlyDictionary<string, string> LegacyNavigationIconMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["new"] = "@iconify:mdi:plus-circle",
        ["content"] = "@iconify:mdi:file-document-multiple",
        ["Content Items"] = "@iconify:mdi:file-document-outline",
        ["Menus"] = "@iconify:mdi:menu",
        ["design"] = "@iconify:mdi:palette",
        ["Content Definition"] = "@iconify:mdi:shape-outline",
        ["Placements"] = "@iconify:mdi:view-dashboard-edit",
        ["Shortcodes"] = "@iconify:mdi:code-braces",
        ["Templates"] = "@iconify:mdi:file-tree",
        ["Themes"] = "@iconify:mdi:theme-light-dark",
        ["Widgets"] = "@iconify:mdi:widgets-outline",
        ["search"] = "@iconify:mdi:magnify",
        ["indexes"] = "@iconify:mdi:database-search",
        ["Indexes"] = "@iconify:mdi:database-search",
        ["Queries"] = "@iconify:mdi:format-list-bulleted",
        ["accessControl"] = "@iconify:mdi:shield-account",
        ["roles"] = "@iconify:mdi:account-key",
        ["Roles"] = "@iconify:mdi:account-key",
        ["users"] = "@iconify:mdi:account-group",
        ["Users"] = "@iconify:mdi:account-group",
        ["media"] = "@iconify:mdi:image-multiple",
        ["Library"] = "@iconify:mdi:folder-image",
        ["Profiles"] = "@iconify:mdi:image-size-select-large",
        ["menu-multitenancy"] = "@iconify:mdi:home-city",
        ["Multi-Tenancy"] = "@iconify:mdi:home-city",
        ["Tenants"] = "@iconify:mdi:home-city",
        ["tools"] = "@iconify:mdi:tools",
        ["Admin Menus"] = "@iconify:mdi:menu",
        ["Deployments"] = "@iconify:mdi:cloud-upload",
        ["Features"] = "@iconify:mdi:shape-outline",
        ["recipes"] = "@iconify:mdi:chef-hat",
        ["Recipes"] = "@iconify:mdi:chef-hat",
        ["settings"] = "@iconify:mdi:cog",
        ["general"] = "@iconify:mdi:cog-outline",
        ["General"] = "@iconify:mdi:cog-outline",
        ["debugging"] = "@iconify:mdi:bug",
        ["Debugging"] = "@iconify:mdi:bug",
        ["admin"] = "@iconify:mdi:shield-crown",
        ["Admin"] = "@iconify:mdi:shield-crown",
        ["Localization"] = "@iconify:mdi:translate",
        ["Security"] = "@iconify:mdi:security",
        ["Zones"] = "@iconify:mdi:map-marker-path"
    };
}
