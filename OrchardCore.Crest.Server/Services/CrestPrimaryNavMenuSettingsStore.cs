using System.Text.Json;
using System.Text.Json.Nodes;
using OrchardCore.Settings;

namespace Crest.Services;

public sealed class CrestPrimaryNavMenuSettingsStore(ISiteService siteService)
{
    private const string SettingsKey = "CrestAdminPrimaryNavMenu";
    private const string PreviousSettingsKey = "CrestAdminSidebar";

    public async ValueTask<CrestPrimaryNavMenuSettings> GetAsync(CancellationToken cancellationToken = default)
    {
        var site = await siteService.LoadSiteSettingsAsync();
        if (site.Properties[SettingsKey] is null && site.Properties.TryGetPropertyValue(PreviousSettingsKey, out var previous))
        {
            // Move the tenant document to its new contract once. Runtime paths
            // thereafter use only the primary-navigation key and shape.
            site.Properties[SettingsKey] = previous?.DeepClone();
            site.Properties.Remove(PreviousSettingsKey);
            await siteService.UpdateSiteSettingsAsync(site);
        }

        return CrestPrimaryNavMenuSettings.Normalize(
            site.Properties[SettingsKey]?.Deserialize<CrestPrimaryNavMenuSettings>(JsonSerializerOptions.Web));
    }

    public async ValueTask<CrestPrimaryNavMenuSettings> SaveAsync(CrestPrimaryNavMenuSettings? settings, CancellationToken cancellationToken = default)
    {
        var normalized = CrestPrimaryNavMenuSettings.Normalize(settings);
        var site = await siteService.LoadSiteSettingsAsync();
        site.Properties[SettingsKey] = JsonSerializer.SerializeToNode(normalized, JsonSerializerOptions.Web) ?? new JsonObject();
        await siteService.UpdateSiteSettingsAsync(site);
        return normalized;
    }
}
