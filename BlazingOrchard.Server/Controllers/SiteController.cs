using Microsoft.AspNetCore.Mvc;
using OrchardCore.Settings;

namespace BlazingOrchard.Controllers;

[ApiController]
[IgnoreAntiforgeryToken]
[Route("api/blazing/site")]
public sealed class SiteController(ISiteService siteService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<SiteSettings>> Get()
    {
        var site = await siteService.GetSiteSettingsAsync();
        return Ok(SiteSettings.From(site));
    }

    [HttpPut]
    public async Task<ActionResult<SiteSettings>> Put(SiteSettingsUpdate update)
    {
        var site = await siteService.LoadSiteSettingsAsync();

        site.SiteName = update.SiteName?.Trim() ?? string.Empty;
        site.PageTitleFormat = update.PageTitleFormat?.Trim() ?? string.Empty;
        site.BaseUrl = update.BaseUrl?.Trim() ?? string.Empty;
        site.TimeZoneId = update.TimeZoneId?.Trim() ?? string.Empty;
        site.Calendar = update.Calendar?.Trim() ?? string.Empty;
        site.PageSize = update.PageSize;
        site.MaxPageSize = update.MaxPageSize;
        site.MaxPagedCount = update.MaxPagedCount;
        site.AppendVersion = update.AppendVersion;
        site.UseCdn = update.UseCdn;
        site.CdnBaseUrl = update.CdnBaseUrl?.Trim() ?? string.Empty;
        site.ResourceDebugMode = ParseEnum(update.ResourceDebugMode, site.ResourceDebugMode);
        site.CacheMode = ParseEnum(update.CacheMode, site.CacheMode);

        await siteService.UpdateSiteSettingsAsync(site);

        return Ok(SiteSettings.From(site));
    }

    private static TEnum ParseEnum<TEnum>(string? value, TEnum fallback)
        where TEnum : struct
        => Enum.TryParse<TEnum>(value, ignoreCase: true, out var result) ? result : fallback;
}

public sealed record SiteSettings(
    string SiteName,
    string PageTitleFormat,
    string BaseUrl,
    string TimeZoneId,
    string Calendar,
    int PageSize,
    int MaxPageSize,
    int MaxPagedCount,
    bool AppendVersion,
    bool UseCdn,
    string CdnBaseUrl,
    string ResourceDebugMode,
    string CacheMode)
{
    public static SiteSettings From(ISite site) => new(
        site.SiteName,
        site.PageTitleFormat,
        site.BaseUrl,
        site.TimeZoneId,
        site.Calendar,
        site.PageSize,
        site.MaxPageSize,
        site.MaxPagedCount,
        site.AppendVersion,
        site.UseCdn,
        site.CdnBaseUrl,
        site.ResourceDebugMode.ToString(),
        site.CacheMode.ToString());
}

public sealed record SiteSettingsUpdate(
    string SiteName,
    string PageTitleFormat,
    string BaseUrl,
    string TimeZoneId,
    string Calendar,
    int PageSize,
    int MaxPageSize,
    int MaxPagedCount,
    bool AppendVersion,
    bool UseCdn,
    string CdnBaseUrl,
    string ResourceDebugMode,
    string CacheMode);
