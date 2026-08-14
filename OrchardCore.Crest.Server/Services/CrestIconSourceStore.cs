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

    public ValueTask<string?> ResolveNavigationItemIconClassAsync(string? itemId, string[] classes, CancellationToken cancellationToken = default)
    {
        foreach (var value in GetLookupValues(itemId, classes))
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

    private static IEnumerable<string> GetLookupValues(string? itemId, IEnumerable<string> classes)
    {
        if (!string.IsNullOrWhiteSpace(itemId))
        {
            yield return itemId;
        }

        foreach (var className in classes.SelectMany(value => value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)))
        {
            yield return className;
        }
    }

    // Keyed by the stable, culture-invariant MenuItem.Id (bare slug, e.g. "content-items"), never by
    // translated caption text: a text-keyed lookup breaks as soon as the UI culture changes.
    private static readonly IReadOnlyDictionary<string, string> LegacyNavigationIconMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["new"] = "@iconify:mdi:plus-circle",
        ["content"] = "@iconify:mdi:file-document-multiple",
        ["content-items"] = "@iconify:mdi:file-document-outline",
        ["menus"] = "@iconify:mdi:menu",
        ["design"] = "@iconify:mdi:palette",
        ["content-definition"] = "@iconify:mdi:shape-outline",
        ["placements"] = "@iconify:mdi:view-dashboard-edit",
        ["shortcodes"] = "@iconify:mdi:code-braces",
        ["templates"] = "@iconify:mdi:file-tree",
        ["themes"] = "@iconify:mdi:theme-light-dark",
        ["widgets"] = "@iconify:mdi:widgets-outline",
        ["search"] = "@iconify:mdi:magnify",
        ["indexes"] = "@iconify:mdi:database-search",
        ["queries"] = "@iconify:mdi:format-list-bulleted",
        ["accessControl"] = "@iconify:mdi:shield-account",
        ["access-control"] = "@iconify:mdi:shield-account",
        ["roles"] = "@iconify:mdi:account-key",
        ["users"] = "@iconify:mdi:account-group",
        ["media"] = "@iconify:mdi:image-multiple",
        ["library"] = "@iconify:mdi:folder-image",
        ["profiles"] = "@iconify:mdi:image-size-select-large",
        ["multi-tenancy"] = "@iconify:mdi:home-city",
        ["tenants"] = "@iconify:mdi:home-city",
        ["tools"] = "@iconify:mdi:tools",
        ["admin-menus"] = "@iconify:mdi:menu",
        ["deployments"] = "@iconify:mdi:cloud-upload",
        ["features"] = "@iconify:mdi:shape-outline",
        ["recipes"] = "@iconify:mdi:chef-hat",
        ["settings"] = "@iconify:mdi:cog",
        ["general"] = "@iconify:mdi:cog-outline",
        ["debugging"] = "@iconify:mdi:bug",
        ["admin"] = "@iconify:mdi:shield-crown",
        ["localization"] = "@iconify:mdi:translate",
        ["security"] = "@iconify:mdi:security",
        ["zones"] = "@iconify:mdi:map-marker-path"
    };
}
