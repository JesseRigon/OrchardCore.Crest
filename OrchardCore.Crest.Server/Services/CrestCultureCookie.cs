using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.Environment.Shell;

namespace Crest.Services;

// Culture resolution happens client-side (Blazor WASM DisplayManager.RefreshManifestAsync
// - see plans/user-localization.md's "Resolution architecture" section): the client is the
// only party that knows the full priority chain (session override, stored user default,
// browser locale, tenant default), so it resolves the winner itself and writes one cookie
// with the final answer. This is deliberately NOT OrchardCore.Localization's
// AdminCookieCultureProvider - that provider only ever answers for requests under the
// admin path prefix, which is wrong here: legacy Orchard pages embedded via
// LegacyAdminFrame.razor (same-origin, same tenant base path) and any other tenant-scoped
// route also need to see the client-resolved culture. So this registers the stock,
// otherwise-unused CookieRequestCultureProvider tenant-wide instead.
//
// This runs as an IPostConfigureOptions, not IConfigureOptions - deliberately. Stock
// OrchardCore.Localization's RequestLocalizationOptionsConfigurations ALSO inserts its
// own provider (AdminCookieCultureProvider) at index 0 via the same
// AddInitialRequestCultureProvider pattern, on the same options instance. Two independent
// Insert(0, ...) calls from two different IConfigureOptions registrations race - ASP.NET
// Core does not guarantee Configure-delegate ordering across separate DI registrations -
// so whichever one happens to run last wins the front slot, and everything behind it
// (including AcceptLanguageHeaderRequestCultureProvider) ends up at an unstable position.
// That's the actual root cause of "Accept-Language sometimes doesn't win" - not a missing
// provider. IPostConfigureOptions is guaranteed to run after every IConfigureOptions, so
// this rebuilds the list deterministically instead of fighting the race: Crest's
// tenant-wide cookie first, then stock Accept-Language as the fallback for a visitor who
// hasn't run the client yet (no cookie set) - exactly what a browser already sends on
// every request, and one of the rungs the client's own chain already treats as
// legitimate. AdminCookieCultureProvider is dropped entirely: Crest's cookie already
// covers everything it covered (tenant-wide, not just /admin), so keeping both is pure
// redundancy. QueryStringRequestCultureProvider is dropped too - nothing in Crest uses it.
public static class CrestCultureCookie
{
    public const string CookieNamePrefix = "crest_culture_";

    public static string MakeCookieName(ShellSettings shellSettings) => CookieNamePrefix + shellSettings.VersionId;

    public static string MakeCookiePath(HttpContext httpContext) => httpContext.Request.PathBase.HasValue
        ? httpContext.Request.PathBase.ToString()
        : "/";

    public static void AddCrestCultureCookieProvider(this IServiceCollection services)
    {
        services.AddTransient<IPostConfigureOptions<RequestLocalizationOptions>, CrestCultureCookieOptionsConfiguration>();
    }
}

internal sealed class CrestCultureCookieOptionsConfiguration(ShellSettings shellSettings) : IPostConfigureOptions<RequestLocalizationOptions>
{
    public void PostConfigure(string? name, RequestLocalizationOptions options)
    {
        options.RequestCultureProviders = [
            new CookieRequestCultureProvider { CookieName = CrestCultureCookie.MakeCookieName(shellSettings) },
            .. options.RequestCultureProviders.Where(provider =>
                provider is AcceptLanguageHeaderRequestCultureProvider),
        ];
    }
}
