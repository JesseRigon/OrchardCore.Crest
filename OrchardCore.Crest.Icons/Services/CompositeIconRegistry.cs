using System.Security.Cryptography;
using System.Text;

namespace Crest.Icons;

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

        if (TryParseLegacyFontAwesomeDeclaration(declaration, out var legacyKeys))
        {
            foreach (var legacyKey in legacyKeys)
            {
                var icon = await ResolveAsync(legacyKey, cancellationToken);
                if (icon is not null)
                {
                    return icon;
                }
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

    private static bool TryParseLegacyFontAwesomeDeclaration(string declaration, out IconKey[] keys)
    {
        keys = [];

        if (string.IsNullOrWhiteSpace(declaration))
        {
            return false;
        }

        var style = FontAwesomeStyle.Auto;
        string? iconName = null;

        foreach (var token in declaration.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var normalized = token.StartsWith("icon-class-", StringComparison.OrdinalIgnoreCase)
                ? token["icon-class-".Length..]
                : token;

            if (TryParseFontAwesomeStyle(normalized, out var parsedStyle))
            {
                style = parsedStyle;
                continue;
            }

            if (normalized.StartsWith("fa-", StringComparison.OrdinalIgnoreCase) && IsLegacyFontAwesomeIconToken(normalized))
            {
                iconName = normalized["fa-".Length..];
            }
        }

        if (string.IsNullOrWhiteSpace(iconName))
        {
            return false;
        }

        keys = GetFontAwesomeCandidatePrefixes(style)
            .Select(prefix => IconKey.Create($"iconify.{prefix}", "current", "default", iconName))
            .Distinct()
            .ToArray();

        return keys.Length > 0;
    }

    private static bool TryParseFontAwesomeStyle(string token, out FontAwesomeStyle style)
    {
        style = token.ToLowerInvariant() switch
        {
            "fa" => FontAwesomeStyle.Auto,
            "fas" or "fa-solid" => FontAwesomeStyle.Solid,
            "far" or "fa-regular" => FontAwesomeStyle.Regular,
            "fab" or "fa-brands" => FontAwesomeStyle.Brands,
            _ => FontAwesomeStyle.None
        };

        return style != FontAwesomeStyle.None;
    }

    private static bool IsLegacyFontAwesomeIconToken(string token)
    {
        var value = token.ToLowerInvariant();

        return value is not (
            "fa-fw" or
            "fa-li" or
            "fa-border" or
            "fa-pull-left" or
            "fa-pull-right" or
            "fa-spin" or
            "fa-pulse" or
            "fa-inverse" or
            "fa-stack" or
            "fa-stack-1x" or
            "fa-stack-2x" or
            "fa-xs" or
            "fa-sm" or
            "fa-lg" or
            "fa-1x" or
            "fa-2x" or
            "fa-3x" or
            "fa-4x" or
            "fa-5x" or
            "fa-6x" or
            "fa-7x" or
            "fa-8x" or
            "fa-9x" or
            "fa-10x");
    }

    private static string[] GetFontAwesomeCandidatePrefixes(FontAwesomeStyle style) => style switch
    {
        FontAwesomeStyle.Solid => ["fa-solid", "fa"],
        FontAwesomeStyle.Regular => ["fa-regular", "fa-solid", "fa"],
        FontAwesomeStyle.Brands => ["fa-brands", "fa"],
        _ => ["fa-solid", "fa-regular", "fa-brands", "fa"]
    };

    private enum FontAwesomeStyle
    {
        None,
        Auto,
        Solid,
        Regular,
        Brands
    }
}
