using System.Collections.Concurrent;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BlazingOrchard.Icons;

public sealed class IconifyIconProvider(
    IHttpClientFactory httpClientFactory,
    IIconProviderSettingsStore settingsStore,
    IIconifyLocalMirrorStore localMirrorStore,
    SvgIconSanitizer svgIconSanitizer) : IIconProvider
{
    private const string ProviderLibraryId = "iconify";
    private static readonly string[] DefaultBrowsePrefixes = ["fa6-solid", "mdi", "lucide", "material-symbols", "ic", "tabler"];
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(10);
    private readonly ConcurrentDictionary<string, CacheEntry<IconAssetDefinition?>> _definitionCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, CacheEntry<IconLibraryDescriptor[]>> _libraryCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, CacheEntry<IconifyCollectionResponse?>> _collectionCache = new(StringComparer.OrdinalIgnoreCase);

    public string Id => "iconify";

    public string Name => "Iconify";

    public async ValueTask<IReadOnlyList<IconLibraryDescriptor>> GetLibrariesAsync(CancellationToken cancellationToken = default)
    {
        var settings = (await settingsStore.GetAsync(cancellationToken)).Iconify;
        if (!settings.Enabled)
        {
            return [];
        }

        var isPublicIconify = localMirrorStore.IsPublicIconify(settings);
        var cacheKey = $"{NormalizeBaseUrl(settings.BaseUrl)}|{string.Join(',', settings.Prefixes.Order(StringComparer.OrdinalIgnoreCase))}";
        if (isPublicIconify && _libraryCache.TryGetValue(cacheKey, out var cached) && cached.ExpiresUtc > DateTimeOffset.UtcNow)
        {
            return cached.Value;
        }

        var libraries = new List<IconLibraryDescriptor> { ProviderLibrary(settings) };
        try
        {
            var localCollections = isPublicIconify ? await localMirrorStore.GetCollectionsAsync(cancellationToken) : new Dictionary<string, IconifyLocalCollectionInfo>(StringComparer.OrdinalIgnoreCase);
            if (localCollections.Count > 0)
            {
                foreach (var prefix in GetVisiblePrefixes(settings, localCollections.Keys))
                {
                    if (localCollections.TryGetValue(prefix, out var info))
                    {
                        libraries.Add(LocalLibrary(prefix, info, settings));
                    }
                    else
                    {
                        libraries.Add(Library(prefix, null, settings));
                    }
                }
            }
            else
            {
                var collections = await GetRemoteCollectionsAsync(settings, cancellationToken);
                foreach (var prefix in GetVisiblePrefixes(settings, collections.Keys))
                {
                    if (collections.TryGetValue(prefix, out var info))
                    {
                        libraries.Add(Library(prefix, info, settings));
                    }
                    else
                    {
                        libraries.Add(Library(prefix, null, settings));
                    }
                }
            }
        }
        catch
        {
            foreach (var prefix in GetVisiblePrefixes(settings, DefaultBrowsePrefixes))
            {
                libraries.Add(Library(prefix, null, settings));
            }
        }

        var result = libraries
            .DistinctBy(library => library.Id, StringComparer.OrdinalIgnoreCase)
            .OrderBy(library => library.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (isPublicIconify)
        {
            _libraryCache[cacheKey] = new(result, DateTimeOffset.UtcNow.Add(CacheDuration));
        }

        return result;
    }

    public async ValueTask<IconAssetDefinition?> ResolveAsync(IconKey key, CancellationToken cancellationToken = default)
    {
        if (!TryGetPrefix(key.Library, out var prefix) || key.Style != "default")
        {
            return null;
        }

        var settings = (await settingsStore.GetAsync(cancellationToken)).Iconify;
        if (!CanUsePrefix(settings, prefix))
        {
            return null;
        }

        return await ResolveIconifyIconAsync(settings, prefix, key.Name, cancellationToken);
    }

    public ValueTask<IconAssetDefinition?> ResolveDeclarationAsync(string declaration, CancellationToken cancellationToken = default)
    {
        if (TryParseProviderReference(declaration, out var providerKey))
        {
            return ResolveAsync(providerKey, cancellationToken);
        }

        if (!IconKey.TryParse(declaration, out var key))
        {
            return ValueTask.FromResult<IconAssetDefinition?>(null);
        }

        return ResolveAsync(key, cancellationToken);
    }

    public async ValueTask<IconSearchResult> SearchAsync(IconSearchRequest request, CancellationToken cancellationToken = default)
    {
        var settings = (await settingsStore.GetAsync(cancellationToken)).Iconify;
        if (!settings.Enabled)
        {
            return Empty(request);
        }

        var requestedLibrary = request.Library?.Trim();
        if (!string.IsNullOrWhiteSpace(requestedLibrary)
            && !string.Equals(requestedLibrary, ProviderLibraryId, StringComparison.OrdinalIgnoreCase)
            && !TryGetPrefix(requestedLibrary, out _))
        {
            return Empty(request);
        }

        try
        {
            var libraries = (await GetLibrariesAsync(cancellationToken)).ToArray();
            var query = request.Query?.Trim();
            if (localMirrorStore.IsPublicIconify(settings))
            {
                var localCollections = await localMirrorStore.GetCollectionsAsync(cancellationToken);
                if (localCollections.Count > 0)
                {
                    return await SearchLocalAsync(settings, request, requestedLibrary, libraries, localCollections, cancellationToken);
                }
            }

            return await SearchRemoteWithLibrariesAsync(settings, request, requestedLibrary, libraries, query, cancellationToken);
        }
        catch
        {
            return Empty(request);
        }
    }

    public async ValueTask<string> GetVersionAsync(CancellationToken cancellationToken = default)
    {
        var settings = (await settingsStore.GetAsync(cancellationToken)).Iconify;
        return $"{NormalizeBaseUrl(settings.BaseUrl)}|{settings.Enabled}|{string.Join(',', settings.Prefixes.Order(StringComparer.OrdinalIgnoreCase))}";
    }

    private async Task<IReadOnlyDictionary<string, IconifyCollectionInfo>> GetRemoteCollectionsAsync(IconifyIconProviderSettings settings, CancellationToken cancellationToken)
    {
        var response = await SendAsync(settings, "collections", cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return new Dictionary<string, IconifyCollectionInfo>(StringComparer.OrdinalIgnoreCase);
        }

        return await response.Content.ReadFromJsonAsync<Dictionary<string, IconifyCollectionInfo>>(cancellationToken: cancellationToken)
            ?? new Dictionary<string, IconifyCollectionInfo>(StringComparer.OrdinalIgnoreCase);
    }

    private async Task<IconSearchResult> SearchLocalAsync(
        IconifyIconProviderSettings settings,
        IconSearchRequest request,
        string? requestedLibrary,
        IconLibraryDescriptor[] libraries,
        IReadOnlyDictionary<string, IconifyLocalCollectionInfo> collections,
        CancellationToken cancellationToken)
    {
        var facets = await BuildLocalFacetsAsync(settings, requestedLibrary, collections, cancellationToken);
        var names = new List<string>();

        if (TryGetPrefix(requestedLibrary, out var prefix))
        {
            if (!CanUsePrefix(settings, prefix))
            {
                return Empty(request);
            }

            var collection = await localMirrorStore.GetCollectionAsync(prefix, cancellationToken);
            if (collection is not null)
            {
                var requestedCategories = GetFilterValues(request, "iconify.icon-category");
                var collectionNames = requestedCategories.Length == 0
                    ? collection.Names
                    : collection.Categories
                        .Where(category => requestedCategories.Contains(category.Key, StringComparer.OrdinalIgnoreCase))
                        .SelectMany(category => category.Value)
                        .Distinct(StringComparer.OrdinalIgnoreCase);

                names.AddRange(collectionNames
                    .Where(name => MatchesQuery(name, request.Query))
                    .Select(name => $"{prefix}:{name}"));
            }
        }
        else
        {
            foreach (var info in GetVisibleLocalCollections(settings, collections).Where(info => MatchesIconSetFilters(info, request)))
            {
                var collection = await localMirrorStore.GetCollectionAsync(info.Prefix, cancellationToken);
                if (collection is null)
                {
                    continue;
                }

                names.AddRange(collection.Names
                    .Where(name => MatchesQuery(name, request.Query))
                    .Select(name => $"{info.Prefix}:{name}"));
            }
        }

        var allNames = names
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var pageNames = allNames
            .Skip(Math.Max(0, request.Skip))
            .Take(ClampTake(request.Take))
            .ToArray();
        var definitions = await ResolveIconifyIconsAsync(settings, pageNames, cancellationToken);
        var items = definitions
            .Select(icon => new IconSearchItem(icon.Key.ToString(), icon.Key.Library, icon.Key.Version, icon.Key.Style, icon.Key.Name, icon.IconClass, icon.SvgMarkup, Id))
            .ToArray();

        return new IconSearchResult(libraries, facets, items, allNames.Length, Math.Max(0, request.Skip), ClampTake(request.Take));
    }

    private async Task<IconSearchResult> SearchRemoteWithLibrariesAsync(
        IconifyIconProviderSettings settings,
        IconSearchRequest request,
        string? requestedLibrary,
        IconLibraryDescriptor[] libraries,
        string? query,
        CancellationToken cancellationToken)
    {
        var collections = await GetRemoteCollectionsAsync(settings, cancellationToken);
        var facets = await BuildFacetsAsync(settings, requestedLibrary, collections, cancellationToken);
        var icons = TryGetPrefix(requestedLibrary, out var prefix)
            ? await BrowsePrefixAsync(settings, prefix, request, cancellationToken)
            : string.IsNullOrWhiteSpace(query)
                ? BrowseDefault(settings, collections, request)
                : await SearchRemoteAsync(settings, requestedLibrary, query, request, collections, cancellationToken);

        var definitions = await ResolveIconifyIconsAsync(settings, icons.Names, cancellationToken);
        var items = definitions
            .Select(icon => new IconSearchItem(icon.Key.ToString(), icon.Key.Library, icon.Key.Version, icon.Key.Style, icon.Key.Name, icon.IconClass, icon.SvgMarkup, Id))
            .ToArray();

        return new IconSearchResult(libraries, facets, items, icons.Total, Math.Max(0, request.Skip), ClampTake(request.Take));
    }

    private async Task<IconifySearchPage> SearchRemoteAsync(
        IconifyIconProviderSettings settings,
        string? requestedLibrary,
        string? query,
        IconSearchRequest request,
        IReadOnlyDictionary<string, IconifyCollectionInfo> collections,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return new([], 0);
        }

        var limit = Math.Clamp(request.Take, 32, 999);
        var path = $"search?query={Uri.EscapeDataString(query)}&start={Math.Max(0, request.Skip)}&limit={limit}";
        if (TryGetPrefix(requestedLibrary, out var prefix))
        {
            if (!CanUsePrefix(settings, prefix))
            {
                return new([], 0);
            }

            path += $"&prefix={Uri.EscapeDataString(prefix)}";
        }
        else if (settings.Prefixes.Length > 0)
        {
            path += $"&prefixes={Uri.EscapeDataString(string.Join(',', settings.Prefixes))}";
        }

        var iconSetCategories = GetFilterValues(request, "iconify.icon-set-category");
        if (iconSetCategories.Length == 1)
        {
            path += $"&category={Uri.EscapeDataString(iconSetCategories[0])}";
        }

        var response = await SendAsync(settings, path, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return new([], 0);
        }

        var search = await response.Content.ReadFromJsonAsync<IconifySearchResponse>(cancellationToken: cancellationToken);
        var names = FilterIconifyNames(search?.Icons ?? [], request, collections);
        return new(names.Take(ClampTake(request.Take)).ToArray(), HasClientSideIconSetFilters(request) ? names.Length : search?.Total ?? names.Length);
    }

    private async Task<IconifySearchPage> BrowsePrefixAsync(IconifyIconProviderSettings settings, string prefix, IconSearchRequest request, CancellationToken cancellationToken)
    {
        if (!CanUsePrefix(settings, prefix))
        {
            return new([], 0);
        }

        var collection = await GetRemoteCollectionAsync(settings, prefix, cancellationToken);
        var names = EnumerateCollectionNames(collection, GetFilterValues(request, "iconify.icon-category"))
            .Where(name => MatchesQuery(name, request.Query))
            .Order(StringComparer.OrdinalIgnoreCase)
            .Select(name => $"{prefix}:{name}")
            .ToArray();

        return new(names.Skip(Math.Max(0, request.Skip)).Take(ClampTake(request.Take)).ToArray(), names.Length);
    }

    private IconifySearchPage BrowseDefault(IconifyIconProviderSettings settings, IReadOnlyDictionary<string, IconifyCollectionInfo> collections, IconSearchRequest request)
    {
        var names = new List<string>();

        foreach (var prefix in GetVisiblePrefixes(settings, collections.Keys))
        {
            if (!CanUsePrefix(settings, prefix) || !collections.TryGetValue(prefix, out var info) || info.Samples is null)
            {
                continue;
            }

            names.AddRange(info.Samples.Select(name => $"{prefix}:{name}"));
        }

        if (names.Count == 0)
        {
            names.AddRange(DefaultBrowsePrefixes.Select(prefix => $"{prefix}:home"));
        }

        var distinct = names.Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase).ToArray();
        return new(distinct.Skip(Math.Max(0, request.Skip)).Take(ClampTake(request.Take)).ToArray(), distinct.Length);
    }

    private async Task<IconifyCollectionResponse?> GetRemoteCollectionAsync(IconifyIconProviderSettings settings, string prefix, CancellationToken cancellationToken)
    {
        var isPublicIconify = localMirrorStore.IsPublicIconify(settings);
        var cacheKey = $"{NormalizeBaseUrl(settings.BaseUrl)}|collection|{prefix}";
        if (isPublicIconify && _collectionCache.TryGetValue(cacheKey, out var cached) && cached.ExpiresUtc > DateTimeOffset.UtcNow)
        {
            return cached.Value;
        }

        var response = await SendAsync(settings, $"collection?prefix={Uri.EscapeDataString(prefix)}", cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            if (isPublicIconify)
            {
                _collectionCache[cacheKey] = new(null, DateTimeOffset.UtcNow.Add(CacheDuration));
            }

            return null;
        }

        var collection = await response.Content.ReadFromJsonAsync<IconifyCollectionResponse>(cancellationToken: cancellationToken);
        if (isPublicIconify)
        {
            _collectionCache[cacheKey] = new(collection, DateTimeOffset.UtcNow.Add(CacheDuration));
        }

        return collection;
    }

    private async Task<IconSearchFacet[]> BuildFacetsAsync(
        IconifyIconProviderSettings settings,
        string? requestedLibrary,
        IReadOnlyDictionary<string, IconifyCollectionInfo> collections,
        CancellationToken cancellationToken)
    {
        var facets = new List<IconSearchFacet>();
        var visibleCollections = collections
            .Where(pair => CanUsePrefix(settings, pair.Key))
            .ToArray();

        AddFacet(
            facets,
            "iconify.icon-set-category",
            "Icon set category",
            visibleCollections
                .Select(pair => pair.Value.Category)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .GroupBy(value => value!, StringComparer.OrdinalIgnoreCase)
                .Select(group => new IconSearchFacetOption(group.Key, group.Key, group.Count())));

        AddFacet(
            facets,
            "iconify.icon-set-tag",
            "Icon set traits",
            visibleCollections
                .SelectMany(pair => pair.Value.Tags ?? [])
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .GroupBy(value => value, StringComparer.OrdinalIgnoreCase)
                .Select(group => new IconSearchFacetOption(group.Key, group.Key, group.Count())));

        AddFacet(
            facets,
            "iconify.palette",
            "Palette",
            visibleCollections
                .GroupBy(pair => pair.Value.Palette ? "multi-color" : "monochrome", StringComparer.OrdinalIgnoreCase)
                .Select(group => new IconSearchFacetOption(group.Key, group.Key == "multi-color" ? "Multi-color" : "Monochrome", group.Count())));

        if (TryGetPrefix(requestedLibrary, out var prefix))
        {
            var collection = await GetRemoteCollectionAsync(settings, prefix, cancellationToken);
            AddFacet(
                facets,
                "iconify.icon-category",
                "Icon category",
                (collection?.Categories ?? new Dictionary<string, string[]>())
                    .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                    .Select(pair => new IconSearchFacetOption(pair.Key, pair.Key, pair.Value.Length)));
        }

        return facets.ToArray();
    }

    private async Task<IconSearchFacet[]> BuildLocalFacetsAsync(
        IconifyIconProviderSettings settings,
        string? requestedLibrary,
        IReadOnlyDictionary<string, IconifyLocalCollectionInfo> collections,
        CancellationToken cancellationToken)
    {
        var facets = new List<IconSearchFacet>();
        var visibleCollections = GetVisibleLocalCollections(settings, collections).ToArray();

        AddFacet(
            facets,
            "iconify.icon-set-category",
            "Icon set category",
            visibleCollections
                .Select(collection => collection.Category)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .GroupBy(value => value!, StringComparer.OrdinalIgnoreCase)
                .Select(group => new IconSearchFacetOption(group.Key, group.Key, group.Count())));

        AddFacet(
            facets,
            "iconify.icon-set-tag",
            "Icon set traits",
            visibleCollections
                .SelectMany(collection => collection.Tags)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .GroupBy(value => value, StringComparer.OrdinalIgnoreCase)
                .Select(group => new IconSearchFacetOption(group.Key, group.Key, group.Count())));

        AddFacet(
            facets,
            "iconify.palette",
            "Palette",
            visibleCollections
                .GroupBy(collection => collection.Palette ? "multi-color" : "monochrome", StringComparer.OrdinalIgnoreCase)
                .Select(group => new IconSearchFacetOption(group.Key, group.Key == "multi-color" ? "Multi-color" : "Monochrome", group.Count())));

        if (TryGetPrefix(requestedLibrary, out var prefix))
        {
            var collection = await localMirrorStore.GetCollectionAsync(prefix, cancellationToken);
            AddFacet(
                facets,
                "iconify.icon-category",
                "Icon category",
                (collection?.Categories ?? new Dictionary<string, string[]>())
                    .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                    .Select(pair => new IconSearchFacetOption(pair.Key, pair.Key, pair.Value.Length)));
        }

        return facets.ToArray();
    }

    private async Task<IconAssetDefinition?> ResolveIconifyIconAsync(IconifyIconProviderSettings settings, string prefix, string name, CancellationToken cancellationToken)
    {
        if (localMirrorStore.IsPublicIconify(settings))
        {
            var local = await localMirrorStore.ResolveAsync(settings, prefix, name, svgIconSanitizer, cancellationToken);
            if (local is not null)
            {
                return local;
            }
        }

        if (!localMirrorStore.IsPublicIconify(settings))
        {
            return (await ResolveIconifyIconsAsync(settings, [$"{prefix}:{name}"], cancellationToken)).FirstOrDefault();
        }

        var cacheKey = $"{NormalizeBaseUrl(settings.BaseUrl)}|{prefix}:{name}";
        if (_definitionCache.TryGetValue(cacheKey, out var cached) && cached.ExpiresUtc > DateTimeOffset.UtcNow)
        {
            return cached.Value;
        }

        var value = (await ResolveIconifyIconsAsync(settings, [$"{prefix}:{name}"], cancellationToken)).FirstOrDefault();
        _definitionCache[cacheKey] = new(value, DateTimeOffset.UtcNow.Add(CacheDuration));
        return value;
    }

    private async Task<IconAssetDefinition[]> ResolveIconifyIconsAsync(IconifyIconProviderSettings settings, IEnumerable<string> iconNames, CancellationToken cancellationToken)
    {
        var isPublicIconify = localMirrorStore.IsPublicIconify(settings);
        var byPrefix = iconNames
            .Select(ParseIconifyName)
            .Where(icon => icon is not null && CanUsePrefix(settings, icon.Value.Prefix))
            .Select(icon => icon!.Value)
            .GroupBy(icon => icon.Prefix, StringComparer.OrdinalIgnoreCase);

        var definitions = new List<IconAssetDefinition>();
        foreach (var group in byPrefix)
        {
            var uncached = new List<string>();
            foreach (var icon in group)
            {
                var cacheKey = $"{NormalizeBaseUrl(settings.BaseUrl)}|{icon.Prefix}:{icon.Name}";
                if (isPublicIconify)
                {
                    var local = await localMirrorStore.ResolveAsync(settings, icon.Prefix, icon.Name, svgIconSanitizer, cancellationToken);
                    if (local is not null)
                    {
                        definitions.Add(local);
                        continue;
                    }
                }

                if (isPublicIconify && _definitionCache.TryGetValue(cacheKey, out var cached) && cached.ExpiresUtc > DateTimeOffset.UtcNow)
                {
                    if (cached.Value is not null)
                    {
                        definitions.Add(cached.Value);
                    }
                }
                else
                {
                    uncached.Add(icon.Name);
                }
            }

            foreach (var names in uncached.Chunk(100))
            {
                var path = $"{Uri.EscapeDataString(group.Key)}.json?icons={Uri.EscapeDataString(string.Join(',', names))}";
                var response = await SendAsync(settings, path, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    continue;
                }

                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
                definitions.AddRange(ParseIconData(settings, group.Key, json.RootElement, names));
            }
        }

        return definitions
            .DistinctBy(icon => icon.Key.ToString(), StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private IEnumerable<IconAssetDefinition> ParseIconData(IconifyIconProviderSettings settings, string prefix, JsonElement root, IEnumerable<string> requestedNames)
    {
        if (!root.TryGetProperty("icons", out var icons) || icons.ValueKind != JsonValueKind.Object)
        {
            yield break;
        }

        var rootWidth = GetInt(root, "width", 16);
        var rootHeight = GetInt(root, "height", 16);
        var requested = requestedNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var icon in icons.EnumerateObject())
        {
            if (!requested.Contains(icon.Name))
            {
                continue;
            }

            var body = GetString(icon.Value, "body");
            if (string.IsNullOrWhiteSpace(body))
            {
                continue;
            }

            var width = GetInt(icon.Value, "width", rootWidth);
            var height = GetInt(icon.Value, "height", rootHeight);
            var left = GetInt(icon.Value, "left", 0);
            var top = GetInt(icon.Value, "top", 0);
            var svg = $"""<svg xmlns="http://www.w3.org/2000/svg" viewBox="{left} {top} {width} {height}" width="1em" height="1em" fill="currentColor" aria-hidden="true" focusable="false">{body}</svg>""";
            if (!svgIconSanitizer.IsSafeSvg(svg))
            {
                continue;
            }

            var key = IconKey.Create($"iconify.{prefix}", "current", "default", icon.Name);
            var definition = new IconAssetDefinition(
                key,
                ToDisplayName(icon.Name),
                key.ToString(),
                svg,
                [icon.Name, prefix, "iconify"],
                Attribution(settings),
                "Iconify collection license");
            if (localMirrorStore.IsPublicIconify(settings))
            {
                _definitionCache[$"{NormalizeBaseUrl(settings.BaseUrl)}|{prefix}:{icon.Name}"] = new(definition, DateTimeOffset.UtcNow.Add(CacheDuration));
            }

            yield return definition;
        }
    }

    private async Task<HttpResponseMessage> SendAsync(IconifyIconProviderSettings settings, string relativePath, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient("BlazingOrchard.Icons.Iconify");
        using var request = new HttpRequestMessage(HttpMethod.Get, BuildUri(settings.BaseUrl, relativePath));
        if (!string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            var header = string.IsNullOrWhiteSpace(settings.ApiKeyHeader) ? "Authorization" : settings.ApiKeyHeader.Trim();
            if (string.Equals(header, "Authorization", StringComparison.OrdinalIgnoreCase))
            {
                request.Headers.TryAddWithoutValidation(header, $"Bearer {settings.ApiKey.Trim()}");
            }
            else
            {
                request.Headers.TryAddWithoutValidation(header, settings.ApiKey.Trim());
            }
        }

        return await client.SendAsync(request, cancellationToken);
    }

    private static Uri BuildUri(string baseUrl, string relativePath)
    {
        var normalizedBase = NormalizeBaseUrl(baseUrl);
        return new Uri($"{normalizedBase}/{relativePath.TrimStart('/')}", UriKind.Absolute);
    }

    private static string NormalizeBaseUrl(string? baseUrl)
    {
        var value = string.IsNullOrWhiteSpace(baseUrl) ? IconifyIconProviderSettings.Default.BaseUrl : baseUrl.Trim();
        return value.TrimEnd('/');
    }

    private static IconLibraryDescriptor ProviderLibrary(IconifyIconProviderSettings settings) => new(
        ProviderLibraryId,
        "Iconify",
        null,
        "iconify",
        "Iconify",
        ["default"],
        ["search", "resolve", "pack", "remote", settings.Prefixes.Length == 0 ? "all-prefixes" : "configured-prefixes"]);

    private static IconLibraryDescriptor Library(string prefix, IconifyCollectionInfo? info, IconifyIconProviderSettings settings) => new(
        $"iconify.{prefix}",
        info?.Name ?? ToDisplayName(prefix),
        info?.Version,
        "iconify",
        "Iconify",
        ["default"],
        ["search", "resolve", "pack", "remote"]);

    private static IconLibraryDescriptor LocalLibrary(string prefix, IconifyLocalCollectionInfo info, IconifyIconProviderSettings settings) => new(
        $"iconify.{prefix}",
        info.Name,
        info.Version,
        "iconify",
        "Iconify",
        ["default"],
        ["search", "resolve", "pack", "local", settings.Prefixes.Length == 0 ? "all-prefixes" : "configured-prefixes"]);

    private static bool TryGetPrefix(string? library, out string prefix)
    {
        prefix = string.Empty;
        if (string.IsNullOrWhiteSpace(library) || !library.StartsWith("iconify.", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        prefix = library["iconify.".Length..].Trim().ToLowerInvariant();
        return prefix.Length > 0;
    }

    private static bool CanUsePrefix(IconifyIconProviderSettings settings, string prefix) =>
        settings.Enabled
        && (settings.Prefixes.Length == 0
            || settings.Prefixes.Contains(prefix, StringComparer.OrdinalIgnoreCase));

    private static string[] GetVisiblePrefixes(IconifyIconProviderSettings settings, IEnumerable<string> availablePrefixes) =>
        settings.Prefixes.Length == 0
            ? availablePrefixes.Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase).ToArray()
            : settings.Prefixes;

    private static IEnumerable<IconifyLocalCollectionInfo> GetVisibleLocalCollections(IconifyIconProviderSettings settings, IReadOnlyDictionary<string, IconifyLocalCollectionInfo> collections)
    {
        var prefixes = GetVisiblePrefixes(settings, collections.Keys).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return collections.Values
            .Where(collection => prefixes.Contains(collection.Prefix))
            .OrderBy(collection => collection.Name, StringComparer.OrdinalIgnoreCase);
    }

    private static bool MatchesIconSetFilters(IconifyLocalCollectionInfo collection, IconSearchRequest request)
    {
        var iconSetTags = GetFilterValues(request, "iconify.icon-set-tag");
        var iconSetCategories = GetFilterValues(request, "iconify.icon-set-category");
        var palettes = GetFilterValues(request, "iconify.palette");
        return (iconSetCategories.Length == 0 || iconSetCategories.Contains(collection.Category, StringComparer.OrdinalIgnoreCase))
            && (iconSetTags.Length == 0 || iconSetTags.All(tag => collection.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase)))
            && (palettes.Length == 0 || palettes.Contains(collection.Palette ? "multi-color" : "monochrome", StringComparer.OrdinalIgnoreCase));
    }

    private static bool TryParseProviderReference(string declaration, out IconKey key)
    {
        key = default;
        declaration = declaration.Trim();
        if (declaration.StartsWith('@'))
        {
            declaration = declaration[1..];
        }

        var parts = declaration.Split(':', 3, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 3 || !string.Equals(parts[0], "iconify", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        key = IconKey.Create($"iconify.{parts[1]}", "current", "default", parts[2]);
        return true;
    }

    private static (string Prefix, string Name)? ParseIconifyName(string value)
    {
        var parts = value.Split(':', 2, StringSplitOptions.TrimEntries);
        return parts.Length == 2 && parts[0].Length > 0 && parts[1].Length > 0
            ? (parts[0].ToLowerInvariant(), parts[1].ToLowerInvariant())
            : null;
    }

    private static string Attribution(IconifyIconProviderSettings settings) => $"Iconify API: {NormalizeBaseUrl(settings.BaseUrl)}";

    private static IconSearchResult Empty(IconSearchRequest request) => new([], [], [], 0, Math.Max(0, request.Skip), ClampTake(request.Take));

    private static int ClampTake(int take) => Math.Clamp(take, 1, 200);

    private static IEnumerable<string> EnumerateCollectionNames(IconifyCollectionResponse? collection, string[]? categories = null)
    {
        if (categories is { Length: > 0 })
        {
            var requested = categories.ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (collection?.Categories is null)
            {
                yield break;
            }

            foreach (var name in collection.Categories
                .Where(category => requested.Contains(category.Key))
                .SelectMany(category => category.Value)
                .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                yield return name;
            }

            yield break;
        }

        if (collection?.Uncategorized is not null)
        {
            foreach (var name in collection.Uncategorized)
            {
                yield return name;
            }
        }

        if (collection?.Categories is not null)
        {
            foreach (var name in collection.Categories.SelectMany(category => category.Value))
            {
                yield return name;
            }
        }
    }

    private static string[] FilterIconifyNames(IEnumerable<string> names, IconSearchRequest request, IReadOnlyDictionary<string, IconifyCollectionInfo> collections)
    {
        var iconSetTags = GetFilterValues(request, "iconify.icon-set-tag");
        var iconSetCategories = GetFilterValues(request, "iconify.icon-set-category");
        var palettes = GetFilterValues(request, "iconify.palette");
        if (iconSetTags.Length == 0 && iconSetCategories.Length <= 1 && palettes.Length == 0)
        {
            return names.ToArray();
        }

        return names
            .Where(value =>
            {
                var parsed = ParseIconifyName(value);
                if (parsed is null || !collections.TryGetValue(parsed.Value.Prefix, out var info))
                {
                    return false;
                }

                return (iconSetCategories.Length <= 1 || iconSetCategories.Contains(info.Category, StringComparer.OrdinalIgnoreCase))
                    && (iconSetTags.Length == 0 || iconSetTags.All(tag => info.Tags?.Contains(tag, StringComparer.OrdinalIgnoreCase) == true))
                    && (palettes.Length == 0 || palettes.Contains(info.Palette ? "multi-color" : "monochrome", StringComparer.OrdinalIgnoreCase));
            })
            .ToArray();
    }

    private static bool HasClientSideIconSetFilters(IconSearchRequest request) =>
        GetFilterValues(request, "iconify.icon-set-tag").Length > 0
        || GetFilterValues(request, "iconify.icon-set-category").Length > 1
        || GetFilterValues(request, "iconify.palette").Length > 0;

    private static string[] GetFilterValues(IconSearchRequest request, string facet) =>
        (request.Filters ?? [])
            .Where(filter => string.Equals(filter.Facet, facet, StringComparison.OrdinalIgnoreCase))
            .Select(filter => filter.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static bool MatchesQuery(string name, string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return true;
        }

        return query
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .All(term => name.Contains(term, StringComparison.OrdinalIgnoreCase));
    }

    private static void AddFacet(List<IconSearchFacet> facets, string id, string label, IEnumerable<IconSearchFacetOption> options)
    {
        var optionArray = options
            .Where(option => !string.IsNullOrWhiteSpace(option.Value))
            .DistinctBy(option => option.Value, StringComparer.OrdinalIgnoreCase)
            .OrderBy(option => option.Label, StringComparer.OrdinalIgnoreCase)
            .Take(80)
            .ToArray();

        if (optionArray.Length > 0)
        {
            facets.Add(new IconSearchFacet(id, label, "multiple", optionArray));
        }
    }

    private static int GetInt(JsonElement element, string property, int fallback) =>
        element.TryGetProperty(property, out var value) && value.TryGetInt32(out var number) ? number : fallback;

    private static string? GetString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static string ToDisplayName(string value) => string.Join(' ', value.Split(['-', '_'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(part => char.ToUpperInvariant(part[0]) + part[1..]));

    private sealed record CacheEntry<T>(T Value, DateTimeOffset ExpiresUtc);

    private sealed record IconifySearchPage(string[] Names, int Total);

    private sealed record IconifySearchResponse(
        [property: JsonPropertyName("icons")] string[]? Icons,
        [property: JsonPropertyName("total")] int Total);

    private sealed record IconifyCollectionResponse(
        [property: JsonPropertyName("uncategorized")] string[]? Uncategorized,
        [property: JsonPropertyName("categories")] Dictionary<string, string[]>? Categories);

    private sealed record IconifyCollectionInfo(
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("version")] string? Version,
        [property: JsonPropertyName("samples")] string[]? Samples,
        [property: JsonPropertyName("category")] string? Category,
        [property: JsonPropertyName("tags")] string[]? Tags,
        [property: JsonPropertyName("palette")] bool Palette);
}
