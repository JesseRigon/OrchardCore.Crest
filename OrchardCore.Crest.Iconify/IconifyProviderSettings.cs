namespace Crest.Iconify;

public sealed record IconifyIconProviderSettings(
    bool Enabled,
    string BaseUrl,
    string? ApiKey,
    string? ApiKeyHeader,
    string[] Prefixes,
    bool LocalLibraryCacheEnabled = true)
{
    public static IconifyIconProviderSettings Default { get; } = new(
        true,
        "https://api.iconify.design",
        null,
        null,
        [],
        true);
}
