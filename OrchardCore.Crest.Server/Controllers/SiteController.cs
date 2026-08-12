using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using OrchardCore.ContentManagement.Routing;
using OrchardCore.Settings;

namespace Crest.Controllers;

[ApiController]
[AutoValidateAntiforgeryToken]
[Route("api/crest/site")]
public sealed class SiteController(
    ISiteService siteService,
    IAuthorizationService authorization,
    IOptions<AutorouteOptions> autorouteOptions) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<SiteSettings>> Get()
    {
        if (!await authorization.AuthorizeAsync(User, SettingsPermissions.ManageSettings)) return Forbid();
        var site = await siteService.GetSiteSettingsAsync();
        return Ok(SiteSettings.From(site));
    }

    // Anonymous, permission-shaped like ContentItemsController.ViewAsync: any caller may
    // ask "what is the site's home content item", the same way any caller may load the
    // home page in a browser. ISite.HomeRoute is written by AutoroutePartHandler when a
    // content item's AutoroutePart.SetHomepage is published (OrchardCore.Autoroute,
    // now a real Crest.Server manifest dependency - see Manifest.cs) - reading it here
    // via IOptions<AutorouteOptions>.ContentItemIdKey ("contentItemId", configured in
    // OrchardCore.Contents/Startup.cs) is the same lookup HomeRouteTransformer performs
    // server-side; no invented literal, just the one real key Orchard itself defines.
    [HttpGet("home")]
    [AllowAnonymous]
    public async Task<ActionResult<SiteHomeResult>> GetHomeAsync()
    {
        var site = await siteService.GetSiteSettingsAsync();
        var contentItemIdKey = autorouteOptions.Value.ContentItemIdKey;

        if (site.HomeRoute is null
            || !site.HomeRoute.TryGetValue(contentItemIdKey, out var contentItemId)
            || contentItemId is not string { Length: > 0 } homeContentItemId)
        {
            return NotFound();
        }

        return Ok(new SiteHomeResult(homeContentItemId));
    }

    [HttpPut]
    public async Task<ActionResult<SiteSettings>> Put(SiteSettingsUpdate update)
    {
        if (!await authorization.AuthorizeAsync(User, SettingsPermissions.ManageSettings)) return Forbid();
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

public sealed record SiteHomeResult(string ContentItemId);
