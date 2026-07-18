using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using BlazingOrchard.Icons;
using Microsoft.Extensions.Logging;

namespace BlazingOrchard.Services;

public sealed record BlazingResolvedIcon(string Library, string? Version, string Name, string? SvgMarkup);

public sealed record BlazingIconLibrary(string Id, string Name, string? Version);

public sealed record BlazingIconCatalogItem(string Library, string? Version, string Name, string IconClass, string? SvgMarkup);

public sealed record BlazingIconSearchResult(BlazingIconLibrary[] Libraries, BlazingIconCatalogItem[] Items, int Total, int Skip, int Take);

public sealed class BlazingIconSourceStore(ILogger<BlazingIconSourceStore> logger)
{
    private static readonly IconSource[] Sources =
    [
        new("fontawesome-free-6.x", "6.6.0", "https://raw.githubusercontent.com/FortAwesome/Font-Awesome/6.x/metadata/icons.json"),
        new("fontawesome-free-5.x", "5.15.4", "https://raw.githubusercontent.com/FortAwesome/Font-Awesome/5.x/metadata/icons.json"),
    ];

    private const string OrchardCoreTag = "v3.0.0";
    private const string OrchardCoreTreeUrl = "https://api.github.com/repos/OrchardCMS/OrchardCore/git/trees/v3.0.0?recursive=1";
    private static readonly Regex NavigationItemIconViewRegex = new(@"/Views/NavigationItemText-(?<id>[^/]+)\.Id\.cshtml$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex IconClassRegex = new("<i\\s+[^>]*class=\\\"(?<class>[^\\\"]+)\\\"", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly string[] VersionSearchOrder = ["6.6.0", "5.15.4"];
    private static readonly string[] StyleSearchOrder = ["fa-solid", "fas", "fa", "fa-regular", "far", "fa-brands", "fab"];

    private readonly SemaphoreSlim _loadLock = new(1, 1);
    private readonly ConcurrentDictionary<string, BlazingResolvedIcon> _icons = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, BlazingIconCatalogItem> _catalog = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, string> _navigationItemIconClasses = new(StringComparer.OrdinalIgnoreCase);
    private bool _loaded;

    public async ValueTask<BlazingIconSearchResult> SearchAsync(string? library, string? query, int skip, int take, CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken);

        skip = Math.Max(0, skip);
        take = Math.Clamp(take, 1, 200);
        var normalizedQuery = query?.Trim();
        var libraries = GetLibraries();
        var items = _catalog.Values
            .Where(item => string.IsNullOrWhiteSpace(library) || string.Equals(GetLibraryId(item.Version), library, StringComparison.OrdinalIgnoreCase))
            .Where(item => string.IsNullOrWhiteSpace(normalizedQuery) || item.Name.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase) || item.IconClass.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase))
            .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Version, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new BlazingIconSearchResult(libraries, items.Skip(skip).Take(take).ToArray(), items.Length, skip, take);
    }

    public async ValueTask<string?> ResolveNavigationItemIconClassAsync(string? itemId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            return null;
        }

        await EnsureLoadedAsync(cancellationToken);
        return _navigationItemIconClasses.TryGetValue(itemId, out var iconClass) ? iconClass : null;
    }

    public async ValueTask<BlazingResolvedIcon?> ResolveIconClassAsync(string iconClass, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(iconClass))
        {
            return null;
        }

        await EnsureLoadedAsync(cancellationToken);

        var (name, version, style) = ParseFontAwesomeClass(iconClass);
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        foreach (var candidateVersion in GetVersionCandidates(version))
        foreach (var candidateStyle in GetStyleCandidates(style))
        {
            if (_icons.TryGetValue($"fontawesome|{candidateVersion}|{candidateStyle}|{name}", out var icon))
            {
                return icon;
            }
        }

        // Local source fallback: the generated registry is compiled into the server so users do not need internet access.
        return IconRegistry.TryResolveIconClass(iconClass, out var local)
            ? new BlazingResolvedIcon(local.Library, local.Version, local.Name, local.SvgMarkup)
            : null;
    }

    private async Task EnsureLoadedAsync(CancellationToken cancellationToken)
    {
        if (_loaded)
        {
            return;
        }

        await _loadLock.WaitAsync(cancellationToken);
        try
        {
            if (_loaded)
            {
                return;
            }

            Directory.CreateDirectory(GetCacheRoot());
            foreach (var source in Sources)
            {
                try
                {
                    var json = await GetSourceJsonAsync(source, cancellationToken);
                    LoadFontAwesomeMetadata(source, json);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogWarning(ex, "Unable to load icon source {IconSourceName} from {IconSourceUrl}.", source.Name, source.Url);
                }
            }

            try
            {
                await LoadOrchardCoreNavigationIconViewsAsync(cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Unable to load OrchardCore admin navigation icon views from {OrchardCoreTreeUrl}.", OrchardCoreTreeUrl);
            }

            _loaded = true;
        }
        finally
        {
            _loadLock.Release();
        }
    }

    private static string GetCacheRoot() => Path.Combine(AppContext.BaseDirectory, "BlazingOrchard.IconSources");

    private static string GetSourcePath(IconSource source) => Path.Combine(GetCacheRoot(), source.Name, "metadata", "icons.json");

    private static async Task<string> GetSourceJsonAsync(IconSource source, CancellationToken cancellationToken)
    {
        var path = GetSourcePath(source);
        if (File.Exists(path))
        {
            return await File.ReadAllTextAsync(path, cancellationToken);
        }

        using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
        var json = await httpClient.GetStringAsync(source.Url, cancellationToken);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, json, cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(GetCacheRoot(), "sources.txt"), string.Join(Environment.NewLine, Sources.Select(value => $"{value.Name} {value.Url}")), cancellationToken);
        return json;
    }

    private void LoadFontAwesomeMetadata(IconSource source, string json)
    {
        using var document = JsonDocument.Parse(json);
        foreach (var iconProperty in document.RootElement.EnumerateObject())
        {
            var iconName = iconProperty.Name;
            if (!iconProperty.Value.TryGetProperty("svg", out var svg))
            {
                continue;
            }

            foreach (var styleProperty in svg.EnumerateObject())
            {
                var style = NormalizeFontAwesomeStyle(styleProperty.Name);
                if (style is null || !styleProperty.Value.TryGetProperty("raw", out var rawProperty))
                {
                    continue;
                }

                var raw = rawProperty.GetString();
                if (string.IsNullOrWhiteSpace(raw))
                {
                    continue;
                }

                AddIcon(source.Version, style, iconName, raw);
            }
        }
    }

    private void AddIcon(string version, string style, string name, string svgMarkup)
    {
        var icon = new BlazingResolvedIcon("fontawesome", version, name, svgMarkup);
        _icons.TryAdd($"fontawesome|{version}|{style}|{name}", icon);
        _catalog.TryAdd($"{GetLibraryId(version)}|{style}|{name}", new BlazingIconCatalogItem(GetLibraryId(version), version, name, $"{style} fa-{name}", svgMarkup));

        foreach (var alias in GetStyleAliases(style))
        {
            _icons.TryAdd($"fontawesome|{version}|{alias}|{name}", icon);
        }
    }

    private static BlazingIconLibrary[] GetLibraries() => Sources
        .Select(source => new BlazingIconLibrary(GetLibraryId(source.Version), source.Name, source.Version))
        .ToArray();

    private static string GetLibraryId(string? version) => version switch
    {
        "5.15.4" => "fontawesome-free-5.x",
        "6.6.0" => "fontawesome-free-6.x",
        _ => "fontawesome",
    };

    private static string? NormalizeFontAwesomeStyle(string style) => style.ToLowerInvariant() switch
    {
        "solid" => "fa-solid",
        "regular" => "fa-regular",
        "brands" => "fa-brands",
        "fa" or "fas" or "fa-solid" => "fa-solid",
        "far" or "fa-regular" => "fa-regular",
        "fab" or "fa-brands" => "fa-brands",
        _ => null,
    };

    private static IEnumerable<string> GetStyleAliases(string style) => style switch
    {
        "fa-solid" => ["fas", "fa"],
        "fa-regular" => ["far"],
        "fa-brands" => ["fab"],
        _ => [],
    };

    private static IEnumerable<string> GetVersionCandidates(string? version) => string.IsNullOrWhiteSpace(version)
        ? VersionSearchOrder
        : [NormalizeVersion(version)];

    private static IEnumerable<string> GetStyleCandidates(string? style) => string.IsNullOrWhiteSpace(style)
        ? StyleSearchOrder
        : [style, .. GetStyleAliases(NormalizeFontAwesomeStyle(style) ?? style)];

    private static string NormalizeVersion(string version) => version switch
    {
        "5" => "5.15.4",
        "6" => "6.6.0",
        _ => version,
    };

    private async Task LoadOrchardCoreNavigationIconViewsAsync(CancellationToken cancellationToken)
    {
        var sourceRoot = Path.Combine(GetCacheRoot(), "orchardcore-admin-navigation-icons", OrchardCoreTag);
        var manifestPath = Path.Combine(sourceRoot, "manifest.json");
        Directory.CreateDirectory(sourceRoot);

        var manifest = File.Exists(manifestPath)
            ? JsonSerializer.Deserialize<NavigationIconViewManifest>(await File.ReadAllTextAsync(manifestPath, cancellationToken))
            : null;

        if (manifest is null)
        {
            manifest = await DownloadOrchardCoreNavigationIconViewManifestAsync(sourceRoot, cancellationToken);
            await File.WriteAllTextAsync(manifestPath, JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }), cancellationToken);
        }

        foreach (var view in manifest.Views)
        {
            var path = Path.Combine(sourceRoot, view.Path.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path))
            {
                continue;
            }

            var content = await File.ReadAllTextAsync(path, cancellationToken);
            var match = IconClassRegex.Match(content);
            if (!match.Success)
            {
                continue;
            }

            var iconClass = match.Groups["class"].Value.Trim();
            if (!string.IsNullOrWhiteSpace(iconClass))
            {
                _navigationItemIconClasses.TryAdd(view.Id, iconClass);
            }
        }
    }

    private static async Task<NavigationIconViewManifest> DownloadOrchardCoreNavigationIconViewManifestAsync(string sourceRoot, CancellationToken cancellationToken)
    {
        using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("BlazingOrchard.IconSources");
        var tree = await httpClient.GetFromJsonAsync<GitHubTreeResponse>(OrchardCoreTreeUrl, cancellationToken) ?? new GitHubTreeResponse([]);
        var views = new List<NavigationIconViewSource>();

        foreach (var item in tree.Tree)
        {
            if (!string.Equals(item.Type, "blob", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var match = NavigationItemIconViewRegex.Match('/' + item.Path);
            if (!match.Success)
            {
                continue;
            }

            var id = match.Groups["id"].Value;
            var url = $"https://raw.githubusercontent.com/OrchardCMS/OrchardCore/{OrchardCoreTag}/{item.Path}";
            var content = await httpClient.GetStringAsync(url, cancellationToken);
            var path = Path.Combine(sourceRoot, item.Path.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllTextAsync(path, content, cancellationToken);
            views.Add(new NavigationIconViewSource(id, item.Path, url));
        }

        await File.AppendAllTextAsync(Path.Combine(GetCacheRoot(), "sources.txt"), Environment.NewLine + string.Join(Environment.NewLine, views.Select(value => $"orchardcore-admin-navigation-icon {value.Url}")), cancellationToken);
        return new NavigationIconViewManifest(views.OrderBy(value => value.Id, StringComparer.OrdinalIgnoreCase).ToArray());
    }

    private static (string? Name, string? Version, string? Style) ParseFontAwesomeClass(string iconClass)
    {
        var classes = iconClass.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(value => value.StartsWith("icon-class-", StringComparison.OrdinalIgnoreCase) ? value["icon-class-".Length..] : value)
            .ToArray();

        string? version = null;
        string? style = null;
        foreach (var className in classes)
        {
            (version, style) = className.ToLowerInvariant() switch
            {
                "fa" => (version, "fa"),
                "fas" => ("5.15.4", "fas"),
                "far" => ("5.15.4", "far"),
                "fab" => ("5.15.4", "fab"),
                "fa-solid" => ("6.6.0", "fa-solid"),
                "fa-regular" => ("6.6.0", "fa-regular"),
                "fa-brands" => ("6.6.0", "fa-brands"),
                _ => (version, style),
            };
        }

        var iconName = classes.LastOrDefault(value => value.StartsWith("fa-", StringComparison.OrdinalIgnoreCase) && !IsFontAwesomeStyleClass(value));
        return (iconName is null ? null : iconName[3..], version, style);
    }

    private static bool IsFontAwesomeStyleClass(string value) =>
        value.Equals("fa-solid", StringComparison.OrdinalIgnoreCase) ||
        value.Equals("fa-regular", StringComparison.OrdinalIgnoreCase) ||
        value.Equals("fa-brands", StringComparison.OrdinalIgnoreCase) ||
        value.Equals("fa-light", StringComparison.OrdinalIgnoreCase) ||
        value.Equals("fa-duotone", StringComparison.OrdinalIgnoreCase) ||
        value.Equals("fa-thin", StringComparison.OrdinalIgnoreCase);

    private sealed record IconSource(string Name, string Version, string Url);

    private sealed record NavigationIconViewManifest(NavigationIconViewSource[] Views);

    private sealed record NavigationIconViewSource(string Id, string Path, string Url);

    private sealed record GitHubTreeResponse([property: JsonPropertyName("tree")] GitHubTreeItem[] Tree);

    private sealed record GitHubTreeItem(
        [property: JsonPropertyName("path")] string Path,
        [property: JsonPropertyName("type")] string Type);
}
