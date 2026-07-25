namespace Crest.Icons;

using Crest.Iconify;

public sealed record CrestIconProvidersSettings(IconifyIconProviderSettings Iconify)
{
    public static CrestIconProvidersSettings Default { get; } = new(IconifyIconProviderSettings.Default);
}

public interface IIconProviderSettingsStore
{
    ValueTask<CrestIconProvidersSettings> GetAsync(CancellationToken cancellationToken = default);

    ValueTask<CrestIconProvidersSettings> SaveAsync(CrestIconProvidersSettings settings, CancellationToken cancellationToken = default);
}
