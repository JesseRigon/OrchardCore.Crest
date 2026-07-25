using System.Text.Json;
using System.Text.Json.Nodes;
using Crest.Iconify;
using Crest.Icons;
using OrchardCore.Settings;

namespace Crest.Services;

public sealed class CrestIconProviderSettingsStore(ISiteService siteService) : IIconProviderSettingsStore
{
    private const string SettingsKey = "CrestIconProviders";

    public async ValueTask<CrestIconProvidersSettings> GetAsync(CancellationToken cancellationToken = default)
    {
        var site = await siteService.GetSiteSettingsAsync();
        return site.Properties[SettingsKey]?.Deserialize<CrestIconProvidersSettings>(JsonSerializerOptions.Web)
            ?? CrestIconProvidersSettings.Default;
    }

    public async ValueTask<CrestIconProvidersSettings> SaveAsync(CrestIconProvidersSettings settings, CancellationToken cancellationToken = default)
    {
        var normalized = Normalize(settings);
        var site = await siteService.LoadSiteSettingsAsync();
        site.Properties[SettingsKey] = JsonSerializer.SerializeToNode(normalized, JsonSerializerOptions.Web) ?? new JsonObject();
        await siteService.UpdateSiteSettingsAsync(site);
        return normalized;
    }

    private static CrestIconProvidersSettings Normalize(CrestIconProvidersSettings settings) => new(Normalize(settings.Iconify));

    private static IconifyIconProviderSettings Normalize(IconifyIconProviderSettings settings) => new(
        settings.Enabled,
        NormalizeUrl(settings.BaseUrl),
        string.IsNullOrWhiteSpace(settings.ApiKey) ? null : settings.ApiKey.Trim(),
        string.IsNullOrWhiteSpace(settings.ApiKeyHeader) ? null : settings.ApiKeyHeader.Trim(),
        settings.Prefixes
            .Where(prefix => !string.IsNullOrWhiteSpace(prefix))
            .Select(prefix => prefix.Trim().ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray(),
        settings.LocalLibraryCacheEnabled);

    private static string NormalizeUrl(string? value)
    {
        var url = string.IsNullOrWhiteSpace(value) ? IconifyIconProviderSettings.Default.BaseUrl : value.Trim();
        return url.TrimEnd('/');
    }
}
