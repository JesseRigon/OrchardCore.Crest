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
public static class CrestCultureCookie
{
    public const string CookieNamePrefix = "crest_culture_";

    public static string MakeCookieName(ShellSettings shellSettings) => CookieNamePrefix + shellSettings.VersionId;

    public static string MakeCookiePath(HttpContext httpContext) => httpContext.Request.PathBase.HasValue
        ? httpContext.Request.PathBase.ToString()
        : "/";

    public static void AddCrestCultureCookieProvider(this IServiceCollection services)
    {
        services.AddTransient<IConfigureOptions<RequestLocalizationOptions>, CrestCultureCookieOptionsConfiguration>();
    }
}

internal sealed class CrestCultureCookieOptionsConfiguration(ShellSettings shellSettings) : IConfigureOptions<RequestLocalizationOptions>
{
    public void Configure(RequestLocalizationOptions options)
    {
        options.RequestCultureProviders.Insert(0, new CookieRequestCultureProvider
        {
            CookieName = CrestCultureCookie.MakeCookieName(shellSettings),
        });
    }
}
