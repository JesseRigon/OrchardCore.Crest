using OrchardCore.Admin.Models;
using OrchardCore.Entities;
using OrchardCore.Settings;

namespace Crest.Services;

public sealed class CrestAdminSettingsNormalizer(ISiteService siteService)
{
    public async Task<AdminSettings> EnsureNewMenuEnabledAsync()
    {
        var site = await siteService.LoadSiteSettingsAsync();
        var settings = site.GetOrCreate<AdminSettings>();

        if (settings.DisplayNewMenu)
        {
            return settings;
        }

        site.Alter<AdminSettings>(adminSettings =>
        {
            adminSettings.DisplayThemeToggler = settings.DisplayThemeToggler;
            adminSettings.DisplayMenuFilter = settings.DisplayMenuFilter;
            adminSettings.DisplayNewMenu = true;
            adminSettings.DisplayTitlesInTopbar = settings.DisplayTitlesInTopbar;
        });

        await siteService.UpdateSiteSettingsAsync(site);

        return site.GetOrCreate<AdminSettings>();
    }
}
