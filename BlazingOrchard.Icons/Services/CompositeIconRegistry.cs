using System.Security.Cryptography;
using System.Text;

namespace BlazingOrchard.Icons;

public sealed class CompositeIconRegistry(IEnumerable<IIconProvider> providers) : IIconRegistry
{
    private readonly IIconProvider[] _providers = providers.ToArray();

    public async ValueTask<IReadOnlyList<IconLibraryDescriptor>> GetLibrariesAsync(CancellationToken cancellationToken = default)
    {
        var libraries = new List<IconLibraryDescriptor>();
        foreach (var provider in _providers)
        {
            libraries.AddRange(await provider.GetLibrariesAsync(cancellationToken));
        }

        return libraries
            .OrderBy(library => library.ProviderName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(library => library.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async ValueTask<IconAssetDefinition?> ResolveAsync(IconKey key, CancellationToken cancellationToken = default)
    {
        foreach (var provider in _providers)
        {
            var icon = await provider.ResolveAsync(key, cancellationToken);
            if (icon is not null)
            {
                return icon;
            }
        }

        return null;
    }

    public async ValueTask<IconAssetDefinition?> ResolveDeclarationAsync(string declaration, CancellationToken cancellationToken = default)
    {
        if (IconKey.TryParse(declaration, out var key))
        {
            return await ResolveAsync(key, cancellationToken);
        }

        foreach (var provider in _providers)
        {
            var icon = await provider.ResolveDeclarationAsync(declaration, cancellationToken);
            if (icon is not null)
            {
                return icon;
            }
        }

        return null;
    }

    public async ValueTask<IconSearchResult> SearchAsync(IconSearchRequest request, CancellationToken cancellationToken = default)
    {
        var providerResults = new List<IconSearchResult>();
        foreach (var provider in _providers)
        {
            providerResults.Add(await provider.SearchAsync(request, cancellationToken));
        }

        var libraries = providerResults
            .SelectMany(result => result.Libraries)
            .DistinctBy(library => library.Id, StringComparer.OrdinalIgnoreCase)
            .OrderBy(library => library.ProviderName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(library => library.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var facets = providerResults
            .SelectMany(result => result.Facets)
            .GroupBy(facet => facet.Id, StringComparer.OrdinalIgnoreCase)
            .Select(group => new IconSearchFacet(
                group.First().Id,
                group.First().Label,
                group.First().SelectionMode,
                group.SelectMany(facet => facet.Options)
                    .GroupBy(option => option.Value, StringComparer.OrdinalIgnoreCase)
                    .Select(optionGroup => new IconSearchFacetOption(
                        optionGroup.First().Value,
                        optionGroup.First().Label,
                        optionGroup.Any(option => option.Count.HasValue) ? optionGroup.Sum(option => option.Count ?? 0) : null))
                    .OrderBy(option => option.Label, StringComparer.OrdinalIgnoreCase)
                    .ToArray()))
            .OrderBy(facet => facet.Label, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var items = providerResults
            .SelectMany(result => result.Items)
            .DistinctBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
            .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Library, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Style, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var total = providerResults.Sum(result => result.Total);
        return new IconSearchResult(libraries, facets, items.Take(Math.Clamp(request.Take, 1, 200)).ToArray(), total, Math.Max(0, request.Skip), Math.Clamp(request.Take, 1, 200));
    }

    public async ValueTask<IconPack> BuildPackAsync(IEnumerable<IconKey> keys, CancellationToken cancellationToken = default)
    {
        var items = new Dictionary<string, IconPackItem>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in keys.Distinct())
        {
            var icon = await ResolveAsync(key, cancellationToken);
            if (icon is null)
            {
                continue;
            }

            items[key.ToString()] = new IconPackItem(
                icon.Key.ToString(),
                icon.Key.Library,
                icon.Key.Version,
                icon.Key.Style,
                icon.Key.Name,
                icon.SvgMarkup,
                icon.Attribution,
                icon.License);
        }

        var versionInput = string.Join('|', items.Keys.Order(StringComparer.OrdinalIgnoreCase));
        var version = items.Count == 0 ? "empty" : Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(versionInput))).ToLowerInvariant();
        return new IconPack(version, items);
    }
}
