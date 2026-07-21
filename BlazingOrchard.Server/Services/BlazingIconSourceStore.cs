using BlazingOrchard.Icons;

namespace BlazingOrchard.Services;

public sealed record BlazingResolvedIcon(string Key, string Library, string? Version, string Style, string Name, string? SvgMarkup);

public sealed record BlazingIconLibrary(string Id, string Name, string? Version, string ProviderId, string ProviderName);

public sealed record BlazingIconSearchFilter(string Facet, string Value);

public sealed record BlazingIconSearchFacet(string Id, string Label, string SelectionMode, BlazingIconSearchFacetOption[] Options);

public sealed record BlazingIconSearchFacetOption(string Value, string Label, int? Count);

public sealed record BlazingIconCatalogItem(string Key, string Library, string? Version, string Style, string Name, string IconClass, string? SvgMarkup, string ProviderId);

public sealed record BlazingIconSearchResult(BlazingIconLibrary[] Libraries, BlazingIconSearchFacet[] Facets, BlazingIconCatalogItem[] Items, int Total, int Skip, int Take);

public sealed class BlazingIconSourceStore(IIconRegistry iconRegistry)
{
    public async ValueTask<BlazingIconSearchResult> SearchAsync(string? library, string? query, int skip, int take, BlazingIconSearchFilter[]? filters = null, CancellationToken cancellationToken = default)
    {
        var result = await iconRegistry.SearchAsync(new IconSearchRequest(
            library,
            query,
            skip,
            take,
            filters?.Select(filter => new IconSearchFilter(filter.Facet, filter.Value)).ToArray()), cancellationToken);
        return new BlazingIconSearchResult(
            result.Libraries.Select(library => new BlazingIconLibrary(library.Id, library.Name, library.Version, library.ProviderId, library.ProviderName)).ToArray(),
            result.Facets.Select(facet => new BlazingIconSearchFacet(
                facet.Id,
                facet.Label,
                facet.SelectionMode,
                facet.Options.Select(option => new BlazingIconSearchFacetOption(option.Value, option.Label, option.Count)).ToArray())).ToArray(),
            result.Items.Select(item => new BlazingIconCatalogItem(item.Key, item.Library, item.Version, item.Style, item.Name, item.IconClass, item.SvgMarkup, item.ProviderId)).ToArray(),
            result.Total,
            result.Skip,
            result.Take);
    }

    public ValueTask<string?> ResolveNavigationItemIconClassAsync(string? itemId, CancellationToken cancellationToken = default) => ValueTask.FromResult<string?>(null);

    public async ValueTask<BlazingResolvedIcon?> ResolveIconClassAsync(string iconClass, CancellationToken cancellationToken = default)
    {
        var icon = await iconRegistry.ResolveDeclarationAsync(iconClass, cancellationToken);
        return icon is null
            ? null
            : new BlazingResolvedIcon(icon.Key.ToString(), icon.Key.Library, icon.Key.Version, icon.Key.Style, icon.Key.Name, icon.SvgMarkup);
    }

    public async ValueTask<IconPack> BuildPackAsync(IEnumerable<string> iconKeys, CancellationToken cancellationToken = default)
    {
        var keys = iconKeys
            .Select(value => IconKey.TryParse(value, out var key) ? key : (IconKey?)null)
            .Where(key => key.HasValue)
            .Select(key => key!.Value);

        return await iconRegistry.BuildPackAsync(keys, cancellationToken);
    }
}
