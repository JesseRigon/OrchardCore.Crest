using System.Text.RegularExpressions;
using OrchardCore.FileStorage;
using OrchardCore.Media;

namespace Crest.Icons;

public sealed partial class TenantMediaIconProvider(
    IMediaFileStore mediaFileStore,
    SvgIconSanitizer svgIconSanitizer) : IIconProvider
{
    public const string LibraryId = "tenant";
    public const string LibraryName = "Tenant Icons";
    public const string RootFolder = "OrchardCore.Crest/Icons";
    public const long MaxSvgBytes = 256 * 1024;

    private static readonly IconLibraryDescriptor[] Libraries =
    [
        new(LibraryId, LibraryName, "current", "tenant-media", "Tenant Media", ["default"], ["resolve", "search", "pack", "upload"])
    ];

    private readonly Lazy<Task<IReadOnlyDictionary<string, IconAssetDefinition>>> _icons = new(() => LoadIconsAsync(mediaFileStore, svgIconSanitizer));

    public string Id => "tenant-media";

    public string Name => "Tenant Media";

    public ValueTask<IReadOnlyList<IconLibraryDescriptor>> GetLibrariesAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult<IReadOnlyList<IconLibraryDescriptor>>(Libraries);

    public async ValueTask<IconAssetDefinition?> ResolveAsync(IconKey key, CancellationToken cancellationToken = default)
    {
        if (!string.Equals(key.Library, LibraryId, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var icons = await _icons.Value;
        return icons.TryGetValue(key.ToString(), out var icon) ? icon : null;
    }

    public async ValueTask<IconAssetDefinition?> ResolveDeclarationAsync(string declaration, CancellationToken cancellationToken = default)
    {
        if (!IconKey.TryParse(declaration, out var key))
        {
            return null;
        }

        return await ResolveAsync(key, cancellationToken);
    }

    public async ValueTask<IconSearchResult> SearchAsync(IconSearchRequest request, CancellationToken cancellationToken = default)
    {
        var query = request.Query?.Trim();
        var icons = (await _icons.Value).Values
            .Where(icon => string.IsNullOrWhiteSpace(request.Library) || string.Equals(icon.Key.Library, request.Library, StringComparison.OrdinalIgnoreCase))
            .Where(icon => string.IsNullOrWhiteSpace(query) || icon.Key.Name.Contains(query, StringComparison.OrdinalIgnoreCase) || icon.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase))
            .OrderBy(icon => icon.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var page = icons
            .Skip(Math.Max(0, request.Skip))
            .Take(Math.Clamp(request.Take, 1, 200))
            .Select(icon => new IconSearchItem(icon.Key.ToString(), icon.Key.Library, icon.Key.Version, icon.Key.Style, icon.Key.Name, icon.IconClass, icon.SvgMarkup, Id))
            .ToArray();

        return new IconSearchResult(Libraries, [], page, icons.Length, Math.Max(0, request.Skip), Math.Clamp(request.Take, 1, 200));
    }

    public ValueTask<string> GetVersionAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult("tenant-media");

    public async Task<IReadOnlyList<TenantMediaIconSummary>> ListAsync(CancellationToken cancellationToken = default)
    {
        var icons = await _icons.Value;
        return icons.Values
            .OrderBy(icon => icon.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(icon => new TenantMediaIconSummary(icon.Key.ToString(), icon.Key.Name, icon.DisplayName, icon.IconClass, GetPath(icon.Key.Name), mediaFileStore.MapPathToPublicUrl(GetPath(icon.Key.Name))))
            .ToArray();
    }

    public async Task<TenantMediaIconSummary> SaveAsync(string fileName, Stream stream, bool overwrite, CancellationToken cancellationToken = default)
    {
        var name = NormalizeIconName(Path.GetFileNameWithoutExtension(fileName));
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidOperationException("Icon file name is required.");
        }

        var svg = await ReadSvgAsync(stream, cancellationToken);
        if (!svgIconSanitizer.IsSafeSvg(svg))
        {
            throw new InvalidOperationException("Only safe SVG icon files are supported.");
        }

        var path = GetPath(name);
        await mediaFileStore.TryCreateDirectoryAsync(RootFolder);
        await using var output = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(svg));
        await mediaFileStore.CreateFileFromStreamAsync(path, output, overwrite);

        var key = IconKey.Create(LibraryId, "current", "default", name);
        return new TenantMediaIconSummary(key.ToString(), key.Name, ToDisplayName(key.Name), key.ToString(), path, mediaFileStore.MapPathToPublicUrl(path));
    }

    public async Task<bool> DeleteAsync(string name, CancellationToken cancellationToken = default)
    {
        var normalizedName = NormalizeIconName(name);
        return !string.IsNullOrWhiteSpace(normalizedName) && await mediaFileStore.TryDeleteFileAsync(GetPath(normalizedName));
    }

    private static async Task<IReadOnlyDictionary<string, IconAssetDefinition>> LoadIconsAsync(IMediaFileStore mediaFileStore, SvgIconSanitizer svgIconSanitizer)
    {
        var icons = new Dictionary<string, IconAssetDefinition>(StringComparer.OrdinalIgnoreCase);
        if (await mediaFileStore.GetDirectoryInfoAsync(RootFolder) is null)
        {
            return icons;
        }

        await foreach (var entry in mediaFileStore.GetDirectoryContentAsync(RootFolder, includeSubDirectories: false))
        {
            if (entry.IsDirectory || !entry.Name.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (entry.Length > MaxSvgBytes)
            {
                continue;
            }

            await using var stream = await mediaFileStore.GetFileStreamAsync(entry);
            using var reader = new StreamReader(stream);
            var svg = await reader.ReadToEndAsync();
            if (!svgIconSanitizer.IsSafeSvg(svg))
            {
                continue;
            }

            var name = NormalizeIconName(Path.GetFileNameWithoutExtension(entry.Name));
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            var key = IconKey.Create(LibraryId, "current", "default", name);
            icons[key.ToString()] = new IconAssetDefinition(key, ToDisplayName(name), key.ToString(), svg, [name, LibraryName], null, "tenant-media");
        }

        return icons;
    }

    private static async Task<string> ReadSvgAsync(Stream stream, CancellationToken cancellationToken)
    {
        using var memory = new MemoryStream();
        await stream.CopyToAsync(memory, cancellationToken);
        if (memory.Length == 0 || memory.Length > MaxSvgBytes)
        {
            throw new InvalidOperationException($"SVG icons must be between 1 byte and {MaxSvgBytes} bytes.");
        }

        return System.Text.Encoding.UTF8.GetString(memory.ToArray());
    }

    private static string GetPath(string name) => $"{RootFolder}/{NormalizeIconName(name)}.svg";

    private static string NormalizeIconName(string value)
    {
        var name = Path.GetFileNameWithoutExtension(value).Trim().ToLowerInvariant();
        name = InvalidNameCharsPattern().Replace(name, "-").Trim('-');
        return DuplicateDashPattern().Replace(name, "-");
    }

    private static string ToDisplayName(string value) => string.Join(' ', value.Split(['-', '_'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(part => char.ToUpperInvariant(part[0]) + part[1..]));

    [GeneratedRegex("[^a-z0-9_-]+", RegexOptions.IgnoreCase)]
    private static partial Regex InvalidNameCharsPattern();

    [GeneratedRegex("-{2,}")]
    private static partial Regex DuplicateDashPattern();
}

public sealed record TenantMediaIconSummary(
    string Key,
    string Name,
    string DisplayName,
    string IconClass,
    string Path,
    string PublicUrl);
