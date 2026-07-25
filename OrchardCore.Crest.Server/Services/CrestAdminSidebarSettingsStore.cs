using System.Text.Json;
using System.Text.Json.Nodes;
using OrchardCore.Settings;

namespace Crest.Services;

public sealed class CrestAdminSidebarSettingsStore(ISiteService siteService)
{
    private const string SettingsKey = "CrestAdminSidebar";

    public async ValueTask<CrestAdminSidebarSettings> GetAsync(CancellationToken cancellationToken = default)
    {
        var site = await siteService.GetSiteSettingsAsync();
        return CrestAdminSidebarSettings.Normalize(
            site.Properties[SettingsKey]?.Deserialize<CrestAdminSidebarSettings>(JsonSerializerOptions.Web));
    }

    public async ValueTask<CrestAdminSidebarSettings> SaveAsync(CrestAdminSidebarSettings? settings, CancellationToken cancellationToken = default)
    {
        var normalized = CrestAdminSidebarSettings.Normalize(settings);
        var site = await siteService.LoadSiteSettingsAsync();
        site.Properties[SettingsKey] = JsonSerializer.SerializeToNode(normalized, JsonSerializerOptions.Web) ?? new JsonObject();
        await siteService.UpdateSiteSettingsAsync(site);
        return normalized;
    }
}
