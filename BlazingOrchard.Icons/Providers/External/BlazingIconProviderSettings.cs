namespace BlazingOrchard.Icons;

public sealed record BlazingIconProvidersSettings(IconifyIconProviderSettings Iconify)
{
    public static BlazingIconProvidersSettings Default { get; } = new(IconifyIconProviderSettings.Default);
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
    ValueTask<BlazingIconProvidersSettings> GetAsync(CancellationToken cancellationToken = default);

    ValueTask<BlazingIconProvidersSettings> SaveAsync(BlazingIconProvidersSettings settings, CancellationToken cancellationToken = default);
}
