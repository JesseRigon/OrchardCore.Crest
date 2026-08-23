using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using OrchardCore.ContentManagement.Routing;
using OrchardCore.Settings;

namespace Crest.ViewModels;

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
