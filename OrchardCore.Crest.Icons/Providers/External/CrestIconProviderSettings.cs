namespace Crest.Icons;

public sealed record CrestIconProvidersSettings(IconifyIconProviderSettings Iconify)
{
    public static CrestIconProvidersSettings Default { get; } = new(IconifyIconProviderSettings.Default);
}

public sealed record IconifyIconProviderSettings(
    bool Enabled,
    string BaseUrl,
    string? ApiKey,
    string? ApiKeyHeader,
    string[] Prefixes)
{
    public static IconifyIconProviderSettings Default { get; } = new(
        true,
        "https://api.iconify.design",
        null,
        null,
        []);
}

public interface IIconProviderSettingsStore
{
    ValueTask<CrestIconProvidersSettings> GetAsync(CancellationToken cancellationToken = default);

    ValueTask<CrestIconProvidersSettings> SaveAsync(CrestIconProvidersSettings settings, CancellationToken cancellationToken = default);
}
