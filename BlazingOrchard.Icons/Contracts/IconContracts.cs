namespace BlazingOrchard.Icons;

public readonly record struct IconKey(string Library, string Version, string Style, string Name)
{
    public override string ToString() => $"{Library}/{Version}/{Style}/{Name}";

    public static bool TryParse(string? value, out IconKey key)
    {
        key = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var parts = value.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 4)
        {
            return false;
        }

        key = Create(parts[0], parts[1], parts[2], parts[3]);
        return true;
    }

    public static IconKey Create(string library, string? version, string? style, string name) => new(
        NormalizePart(library),
        NormalizePart(string.IsNullOrWhiteSpace(version) ? "current" : version),
        NormalizePart(string.IsNullOrWhiteSpace(style) ? "default" : style),
        NormalizeName(name));

    private static string NormalizePart(string value) => value.Trim().ToLowerInvariant();

    private static string NormalizeName(string value) => value.Trim().ToLowerInvariant();
}

public sealed record IconLibraryDescriptor(
    string Id,
    string Name,
    string? Version,
    string ProviderId,
    string ProviderName,
    string[] Styles,
    string[] Capabilities);

public sealed record IconAssetDefinition(
    IconKey Key,
    string DisplayName,
    string IconClass,
    string SvgMarkup,
    string[] Tags,
    string? Attribution = null,
    string? License = null);

public sealed record IconPack(
    string Version,
    IReadOnlyDictionary<string, IconPackItem> Icons)
{
    public static IconPack Empty { get; } = new("empty", new Dictionary<string, IconPackItem>(StringComparer.OrdinalIgnoreCase));
}

public sealed record IconPackItem(
    string Key,
    string Library,
    string Version,
    string Style,
    string Name,
    string SvgMarkup,
    string? Attribution = null,
    string? License = null);

public sealed record IconSearchRequest(
    string? Library,
    string? Query,
    int Skip,
    int Take);

public sealed record IconSearchResult(
    IconLibraryDescriptor[] Libraries,
    IconSearchItem[] Items,
    int Total,
    int Skip,
    int Take);

public sealed record IconSearchItem(
    string Key,
    string Library,
    string? Version,
    string Style,
    string Name,
    string IconClass,
    string? SvgMarkup,
    string ProviderId);
