using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;

namespace BlazingOrchard.Icons;

public interface IIconifyLocalMirrorPathProvider
{
    string RootPath { get; }

    string SeedPath { get; }
}

public interface IIconifyLocalMirrorStore
{
    ValueTask<IconifyLocalMirrorStatus> GetStatusAsync(CancellationToken cancellationToken = default);

    ValueTask<IconifyLocalMirrorStatus> SyncAsync(CancellationToken cancellationToken = default);

    ValueTask<IReadOnlyDictionary<string, IconifyLocalCollectionInfo>> GetCollectionsAsync(CancellationToken cancellationToken = default);

    ValueTask<IconifyLocalCollection?> GetCollectionAsync(string prefix, CancellationToken cancellationToken = default);

    ValueTask<IconAssetDefinition?> ResolveAsync(IconifyIconProviderSettings settings, string prefix, string name, SvgIconSanitizer sanitizer, CancellationToken cancellationToken = default);

    bool IsPublicIconify(IconifyIconProviderSettings settings);
}

public sealed record IconifyLocalMirrorStatus(
    bool IsAvailable,
    bool IsSyncing,
    string? Version,
    string RootPath,
    string? SourcePath,
    int PrefixCount,
    int IconCount,
    DateTimeOffset? LastSyncUtc,
    DateTimeOffset? LastErrorUtc,
    string? LastError,
    bool RemoteFallbackEnabled = true);

public sealed record IconifyLocalCollectionInfo(
    string Prefix,
    string Name,
    string? Version,
    int Total,
    string? Category,
    string[] Tags,
    bool Palette,
    string[] Samples,
    string? License,
    string? Attribution);

public sealed record IconifyLocalCollection(
    IconifyLocalCollectionInfo Info,
    string[] Names,
    IReadOnlyDictionary<string, string[]> Categories);

public sealed class IconifyLocalMirrorStore(IIconifyLocalMirrorPathProvider pathProvider) : IIconifyLocalMirrorStore
{
    private const string RepositoryUrl = "https://github.com/iconify/icon-sets.git";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly SemaphoreSlim SyncLock = new(1, 1);
    private readonly ConcurrentDictionary<string, IconifyLocalCollection> _collectionCache = new(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyDictionary<string, IconifyLocalCollectionInfo>? _collectionsCache;
    private IconifyLocalMirrorMetadata _metadata = IconifyLocalMirrorMetadata.Empty;
    private bool _isSyncing;

    public bool IsPublicIconify(IconifyIconProviderSettings settings) =>
        string.Equals(NormalizeBaseUrl(settings.BaseUrl), NormalizeBaseUrl(IconifyIconProviderSettings.Default.BaseUrl), StringComparison.OrdinalIgnoreCase);

    public async ValueTask<IconifyLocalMirrorStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        var sourcePath = GetCurrentSourcePath();
        var collections = await TryGetCollectionsAsync(cancellationToken);
        var metadata = await ReadMetadataAsync(cancellationToken);
        var packageVersion = await ReadPackageVersionAsync(RootPath, cancellationToken);
        return new IconifyLocalMirrorStatus(
            sourcePath is not null && collections.Count > 0,
            _isSyncing,
            packageVersion ?? metadata.Version,
            RootPath,
            sourcePath,
            collections.Count,
            collections.Values.Sum(collection => collection.Total),
            metadata.LastSyncUtc,
            metadata.LastErrorUtc,
            metadata.LastError);
    }

    public async ValueTask<IconifyLocalMirrorStatus> SyncAsync(CancellationToken cancellationToken = default)
    {
        await SyncLock.WaitAsync(cancellationToken);
        _isSyncing = true;
        try
        {
            await RefreshRuntimeCacheAsync(cancellationToken);
            await RefreshSeedSubmoduleAsync(cancellationToken);
            _metadata = new IconifyLocalMirrorMetadata(
                await ReadPackageVersionAsync(RootPath, cancellationToken),
                DateTimeOffset.UtcNow,
                null,
                null);
            await WriteMetadataAsync(_metadata, cancellationToken);
            _collectionsCache = null;
            _collectionCache.Clear();
        }
        catch (Exception ex)
        {
            var existing = await ReadMetadataAsync(cancellationToken);
            _metadata = existing with
            {
                LastErrorUtc = DateTimeOffset.UtcNow,
                LastError = ex.Message,
            };
            await WriteMetadataAsync(_metadata, cancellationToken);
        }
        finally
        {
            _isSyncing = false;
            SyncLock.Release();
        }

        return await GetStatusAsync(cancellationToken);
    }

    public async ValueTask<IReadOnlyDictionary<string, IconifyLocalCollectionInfo>> GetCollectionsAsync(CancellationToken cancellationToken = default)
    {
        if (_collectionsCache is not null)
        {
            return _collectionsCache;
        }

        await EnsureInitializedAsync(cancellationToken);
        _collectionsCache = await TryGetCollectionsAsync(cancellationToken);
        return _collectionsCache;
    }

    public async ValueTask<IconifyLocalCollection?> GetCollectionAsync(string prefix, CancellationToken cancellationToken = default)
    {
        prefix = NormalizePrefix(prefix);
        if (string.IsNullOrWhiteSpace(prefix))
        {
            return null;
        }

        if (_collectionCache.TryGetValue(prefix, out var cached))
        {
            return cached;
        }

        await EnsureInitializedAsync(cancellationToken);
        var sourcePath = GetCurrentSourcePath();
        if (sourcePath is null)
        {
            return null;
        }

        var path = GetCollectionPath(sourcePath, prefix);
        if (!File.Exists(path))
        {
            return null;
        }

        await using var stream = File.OpenRead(path);
        using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var root = json.RootElement;
        var collections = await GetCollectionsAsync(cancellationToken);
        if (!collections.TryGetValue(prefix, out var info))
        {
            info = CollectionInfoFromRoot(prefix, root);
        }

        var names = EnumerateNames(root).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase).ToArray();
        var categories = root.TryGetProperty("categories", out var categoryElement) && categoryElement.ValueKind == JsonValueKind.Object
            ? categoryElement.EnumerateObject().ToDictionary(
                category => category.Name,
                category => category.Value.ValueKind == JsonValueKind.Array
                    ? category.Value.EnumerateArray().Select(value => value.GetString()).Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value!).ToArray()
                    : [],
                StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        var collection = new IconifyLocalCollection(info, names, categories);
        _collectionCache[prefix] = collection;
        return collection;
    }

    public async ValueTask<IconAssetDefinition?> ResolveAsync(IconifyIconProviderSettings settings, string prefix, string name, SvgIconSanitizer sanitizer, CancellationToken cancellationToken = default)
    {
        if (!IsPublicIconify(settings))
        {
            return null;
        }

        prefix = NormalizePrefix(prefix);
        name = name.Trim().ToLowerInvariant();
        await EnsureInitializedAsync(cancellationToken);
        var sourcePath = GetCurrentSourcePath();
        if (sourcePath is null)
        {
            return null;
        }

        var path = GetCollectionPath(sourcePath, prefix);
        if (!File.Exists(path))
        {
            return null;
        }

        await using var stream = File.OpenRead(path);
        using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var root = json.RootElement;
        if (!TryGetIconElement(root, name, out var iconElement))
        {
            return null;
        }

        var body = GetString(iconElement, "body");
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        var rootWidth = GetInt(root, "width", 16);
        var rootHeight = GetInt(root, "height", 16);
        var width = GetInt(iconElement, "width", rootWidth);
        var height = GetInt(iconElement, "height", rootHeight);
        var left = GetInt(iconElement, "left", 0);
        var top = GetInt(iconElement, "top", 0);
        var svg = $"""<svg xmlns="http://www.w3.org/2000/svg" viewBox="{left} {top} {width} {height}" width="1em" height="1em" fill="currentColor" aria-hidden="true" focusable="false">{body}</svg>""";
        if (!sanitizer.IsSafeSvg(svg))
        {
            return null;
        }

        var key = IconKey.Create($"iconify.{prefix}", "current", "default", name);
        var collections = await GetCollectionsAsync(cancellationToken);
        collections.TryGetValue(prefix, out var info);
        return new IconAssetDefinition(
            key,
            ToDisplayName(name),
            key.ToString(),
            svg,
            [name, prefix, "iconify"],
            info?.Attribution ?? "Iconify local cache",
            info?.License ?? "Iconify collection license");
    }

    private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (File.Exists(Path.Combine(RootPath, "collections.json")))
        {
            return;
        }

        await SyncLock.WaitAsync(cancellationToken);
        try
        {
            if (File.Exists(Path.Combine(RootPath, "collections.json")))
            {
                return;
            }

            if (File.Exists(Path.Combine(SeedPath, "collections.json")))
            {
                await CopySeedToRuntimeCacheAsync(cancellationToken);
                return;
            }
        }
        finally
        {
            SyncLock.Release();
        }
    }

    private async Task CopySeedToRuntimeCacheAsync(CancellationToken cancellationToken)
    {
        var tempPath = Path.Combine(Path.GetDirectoryName(RootPath) ?? RootPath, ".iconify-cache-seed-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempPath);
        try
        {
            CopyFileIfExists(Path.Combine(SeedPath, "collections.json"), Path.Combine(tempPath, "collections.json"));
            CopyFileIfExists(Path.Combine(SeedPath, "package.json"), Path.Combine(tempPath, "package.json"));
            CopyDirectory(Path.Combine(SeedPath, "json"), Path.Combine(tempPath, "json"));
            Directory.CreateDirectory(Path.GetDirectoryName(RootPath)!);
            if (Directory.Exists(RootPath))
            {
                Directory.Move(RootPath, RootPath + ".old-" + DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            }

            Directory.Move(tempPath, RootPath);
            _metadata = new IconifyLocalMirrorMetadata(
                await ReadPackageVersionAsync(RootPath, cancellationToken),
                DateTimeOffset.UtcNow,
                null,
                null);
            await WriteMetadataAsync(_metadata, cancellationToken);
            _collectionsCache = null;
            _collectionCache.Clear();
        }
        catch
        {
            if (Directory.Exists(tempPath))
            {
                Directory.Delete(tempPath, recursive: true);
            }

            throw;
        }
    }

    private async Task RefreshRuntimeCacheAsync(CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(RootPath)!);
        if (File.Exists(Path.Combine(RootPath, ".git")) || Directory.Exists(Path.Combine(RootPath, ".git")))
        {
            await UpdateGitCheckoutAsync(RootPath, cancellationToken);
            await RunGitAsync("sparse-checkout set --no-cone /collections.json /json/", RootPath, cancellationToken);
            return;
        }

        var tempPath = Path.Combine(Path.GetDirectoryName(RootPath)!, ".iconify-cache-refresh-" + Guid.NewGuid().ToString("N"));
        try
        {
            var referenceArgument = Directory.Exists(Path.Combine(SeedPath, ".git")) || File.Exists(Path.Combine(SeedPath, ".git"))
                ? $" --reference-if-able {SeedPath}"
                : string.Empty;
            await RunGitAsync($"clone --depth 1 --filter=blob:none --sparse{referenceArgument} {RepositoryUrl} {tempPath}", Path.GetDirectoryName(RootPath)!, cancellationToken);
            await RunGitAsync("sparse-checkout set --no-cone /collections.json /json/", tempPath, cancellationToken);

            if (Directory.Exists(RootPath))
            {
                var previousPath = RootPath + ".old-" + DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                Directory.Move(RootPath, previousPath);
                Directory.Delete(previousPath, recursive: true);
            }

            Directory.Move(tempPath, RootPath);
        }
        catch
        {
            if (Directory.Exists(tempPath))
            {
                Directory.Delete(tempPath, recursive: true);
            }

            throw;
        }
    }

    private async Task RefreshSeedSubmoduleAsync(CancellationToken cancellationToken)
    {
        if (!Directory.Exists(SeedPath))
        {
            return;
        }

        if (!File.Exists(Path.Combine(SeedPath, ".git")) && !Directory.Exists(Path.Combine(SeedPath, ".git")))
        {
            return;
        }

        await UpdateGitCheckoutAsync(SeedPath, cancellationToken);
        await RunGitAsync("sparse-checkout set --no-cone /collections.json /json/", SeedPath, cancellationToken);
    }

    private static async Task UpdateGitCheckoutAsync(string workingDirectory, CancellationToken cancellationToken)
    {
        await RunGitAsync("fetch --depth 1 origin master", workingDirectory, cancellationToken);
        await RunGitAsync("checkout --force FETCH_HEAD", workingDirectory, cancellationToken);
    }

    private async Task<IReadOnlyDictionary<string, IconifyLocalCollectionInfo>> TryGetCollectionsAsync(CancellationToken cancellationToken)
    {
        var sourcePath = GetCurrentSourcePath();
        return sourcePath is null
            ? new Dictionary<string, IconifyLocalCollectionInfo>(StringComparer.OrdinalIgnoreCase)
            : await ReadCollectionsFromPathAsync(sourcePath, cancellationToken);
    }

    private async Task<IReadOnlyDictionary<string, IconifyLocalCollectionInfo>> ReadCollectionsFromPathAsync(string sourcePath, CancellationToken cancellationToken)
    {
        var path = Path.Combine(sourcePath, "collections.json");
        if (!File.Exists(path))
        {
            return new Dictionary<string, IconifyLocalCollectionInfo>(StringComparer.OrdinalIgnoreCase);
        }

        await using var stream = File.OpenRead(path);
        using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var collections = new Dictionary<string, IconifyLocalCollectionInfo>(StringComparer.OrdinalIgnoreCase);
        foreach (var collection in json.RootElement.EnumerateObject())
        {
            var prefix = NormalizePrefix(collection.Name);
            collections[prefix] = CollectionInfoFromMetadata(prefix, collection.Value);
        }

        return collections;
    }

    private static async Task RunGitAsync(string arguments, string workingDirectory, CancellationToken cancellationToken)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo("git", arguments)
            {
                WorkingDirectory = workingDirectory,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            },
        };

        if (!process.Start())
        {
            throw new InvalidOperationException("Could not start git.");
        }

        var stdout = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderr = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
        {
            var output = ((await stdout) + Environment.NewLine + (await stderr)).Trim();
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(output) ? $"git {arguments} failed with exit code {process.ExitCode}." : output);
        }
    }

    private string? GetCurrentSourcePath()
    {
        var rootPath = RootPath;
        return File.Exists(Path.Combine(rootPath, "collections.json")) ? rootPath : null;
    }

    private async Task<IconifyLocalMirrorMetadata> ReadMetadataAsync(CancellationToken cancellationToken)
    {
        if (_metadata != IconifyLocalMirrorMetadata.Empty)
        {
            return _metadata;
        }

        var path = MetadataPath;
        if (!File.Exists(path))
        {
            return IconifyLocalMirrorMetadata.Empty;
        }

        await using var stream = File.OpenRead(path);
        _metadata = await JsonSerializer.DeserializeAsync<IconifyLocalMirrorMetadata>(stream, JsonOptions, cancellationToken)
            ?? IconifyLocalMirrorMetadata.Empty;
        return _metadata;
    }

    private async Task WriteMetadataAsync(IconifyLocalMirrorMetadata metadata, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(RootPath);
        await using var stream = File.Create(MetadataPath);
        await JsonSerializer.SerializeAsync(stream, metadata, JsonOptions, cancellationToken);
    }

    private static async Task<string?> ReadPackageVersionAsync(string rootPath, CancellationToken cancellationToken)
    {
        var packagePath = Path.Combine(rootPath, "package.json");
        if (!File.Exists(packagePath))
        {
            return null;
        }

        await using var stream = File.OpenRead(packagePath);
        using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        return GetString(json.RootElement, "version");
    }

    private string RootPath => pathProvider.RootPath;

    private string SeedPath => pathProvider.SeedPath;

    private string MetadataPath => Path.Combine(RootPath, ".blazing-orchard-cache.json");

    private static string GetCollectionPath(string sourcePath, string prefix) => Path.Combine(sourcePath, "json", NormalizePrefix(prefix) + ".json");

    private static void CopyFileIfExists(string sourcePath, string destinationPath)
    {
        if (!File.Exists(sourcePath))
        {
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
        File.Copy(sourcePath, destinationPath, overwrite: true);
    }

    private static void CopyDirectory(string sourcePath, string destinationPath)
    {
        if (!Directory.Exists(sourcePath))
        {
            return;
        }

        foreach (var directory in Directory.EnumerateDirectories(sourcePath, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(Path.Combine(destinationPath, Path.GetRelativePath(sourcePath, directory)));
        }

        foreach (var file in Directory.EnumerateFiles(sourcePath, "*", SearchOption.AllDirectories))
        {
            var destination = Path.Combine(destinationPath, Path.GetRelativePath(sourcePath, file));
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(file, destination, overwrite: true);
        }
    }

    private static IconifyLocalCollectionInfo CollectionInfoFromMetadata(string prefix, JsonElement element)
    {
        var license = element.TryGetProperty("license", out var licenseElement) && licenseElement.ValueKind == JsonValueKind.Object
            ? GetString(licenseElement, "spdx") ?? GetString(licenseElement, "title")
            : null;
        var author = element.TryGetProperty("author", out var authorElement) && authorElement.ValueKind == JsonValueKind.Object
            ? GetString(authorElement, "name")
            : null;
        return new IconifyLocalCollectionInfo(
            prefix,
            GetString(element, "name") ?? ToDisplayName(prefix),
            GetString(element, "version"),
            GetInt(element, "total", 0),
            GetString(element, "category"),
            GetStringArray(element, "tags"),
            element.TryGetProperty("palette", out var palette) && palette.ValueKind == JsonValueKind.True,
            GetStringArray(element, "samples"),
            license,
            author is null ? null : $"Iconify local cache: {author}");
    }

    private static IconifyLocalCollectionInfo CollectionInfoFromRoot(string prefix, JsonElement root) => new(
        prefix,
        GetString(root, "title") ?? ToDisplayName(prefix),
        GetString(root, "version"),
        GetInt(root, "total", 0),
        null,
        [],
        false,
        [],
        null,
        "Iconify local cache");

    private static IEnumerable<string> EnumerateNames(JsonElement root)
    {
        if (root.TryGetProperty("icons", out var icons) && icons.ValueKind == JsonValueKind.Object)
        {
            foreach (var icon in icons.EnumerateObject())
            {
                yield return icon.Name;
            }
        }

        if (root.TryGetProperty("aliases", out var aliases) && aliases.ValueKind == JsonValueKind.Object)
        {
            foreach (var alias in aliases.EnumerateObject())
            {
                yield return alias.Name;
            }
        }
    }

    private static bool TryGetIconElement(JsonElement root, string name, out JsonElement iconElement)
    {
        iconElement = default;
        if (root.TryGetProperty("icons", out var icons) && icons.ValueKind == JsonValueKind.Object && icons.TryGetProperty(name, out iconElement))
        {
            return true;
        }

        if (!root.TryGetProperty("aliases", out var aliases) || aliases.ValueKind != JsonValueKind.Object || !aliases.TryGetProperty(name, out var alias))
        {
            return false;
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { name };
        while (alias.TryGetProperty("parent", out var parentElement))
        {
            var parent = parentElement.GetString();
            if (string.IsNullOrWhiteSpace(parent) || !seen.Add(parent))
            {
                return false;
            }

            if (icons.ValueKind == JsonValueKind.Object && icons.TryGetProperty(parent, out iconElement))
            {
                return true;
            }

            if (!aliases.TryGetProperty(parent, out alias))
            {
                return false;
            }
        }

        return false;
    }

    private static int GetInt(JsonElement element, string property, int fallback) =>
        element.TryGetProperty(property, out var value) && value.TryGetInt32(out var number) ? number : fallback;

    private static string? GetString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static string[] GetStringArray(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Array
            ? value.EnumerateArray().Select(item => item.GetString()).Where(item => !string.IsNullOrWhiteSpace(item)).Select(item => item!).ToArray()
            : [];

    private static string NormalizePrefix(string value) => value.Trim().ToLowerInvariant();

    private static string NormalizeBaseUrl(string? baseUrl)
    {
        var value = string.IsNullOrWhiteSpace(baseUrl) ? IconifyIconProviderSettings.Default.BaseUrl : baseUrl.Trim();
        return value.TrimEnd('/');
    }

    private static string ToDisplayName(string value) => string.Join(' ', value.Split(['-', '_'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(part => char.ToUpperInvariant(part[0]) + part[1..]));

    private sealed record IconifyLocalMirrorMetadata(
        string? Version,
        DateTimeOffset? LastSyncUtc,
        DateTimeOffset? LastErrorUtc,
        string? LastError)
    {
        public static IconifyLocalMirrorMetadata Empty { get; } = new(null, null, null, null);
    }
}
