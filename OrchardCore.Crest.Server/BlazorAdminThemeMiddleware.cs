using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Crest.Services;
using OrchardCore.Admin;
using OrchardCore.Environment.Shell;
using OrchardCore.Environment.Shell.Scope;

namespace Crest;

public sealed class BlazorAdminThemeOptions
{
    // Defaults match AdminOptions.AdminUrlPrefix / UserOptions.LoginPath's own stock
    // defaults ("Admin" / "Login") - BlazorAdminThemeOptionsConfiguration below
    // overrides these from the tenant's real, configured values (recipe/appsettings,
    // "OrchardCore_Admin"/"OrchardCore_Users" shell config sections) so a tenant that
    // customizes either path doesn't silently break admin-theme routing. These
    // property defaults only apply if that PostConfigure step is somehow skipped.
    public string AdminPath { get; set; } = "/admin";
    public string LoginPath { get; set; } = "/login";
    public string LogoutPath { get; set; } = "/users/logoff";
    public string BlazorThemeTag { get; set; } = "blazor";
    public string BlazorAdminThemeId { get; set; } = "OrchardCore.Crest.Admin";
}

// Keeps BlazorAdminThemeOptions.AdminPath/LoginPath/LogoutPath in sync with Orchard's own,
// real, tenant-configurable settings (AdminOptions.AdminUrlPrefix, UserOptions.LoginPath,
// UserOptions.LogoffPath - all bound from shell config, e.g. a recipe's
// "OrchardCore_Admin"/"OrchardCore_Users" sections) instead of Crest hardcoding its
// own copies that silently drift if a tenant customizes any of them. Runs as
// IPostConfigureOptions so it applies after BlazorAdminThemeOptions' own
// IConfigureOptions (currently just the no-op in Startup.cs, but this keeps the
// override deterministic regardless of registration order - see
// CrestCultureCookieOptionsConfiguration for the same pattern and its rationale).
internal sealed class BlazorAdminThemeOptionsConfiguration(
    IOptions<AdminOptions> adminOptions,
    IOptions<OrchardCore.Users.UserOptions> userOptions) : IPostConfigureOptions<BlazorAdminThemeOptions>
{
    public void PostConfigure(string? name, BlazorAdminThemeOptions options)
    {
        options.AdminPath = "/" + adminOptions.Value.AdminUrlPrefix;
        options.LoginPath = "/" + userOptions.Value.LoginPath;
        options.LogoutPath = "/" + userOptions.Value.LogoffPath;
    }
}

// Phase 8: this middleware no longer serves anything itself. The old WASM-SPA model
// (hand-serving index.html with a rewritten <base href> plus every framework/theme
// asset out of the wasm project's build webroot) is retired - Crest.Server's
// MapRazorComponents<App>() endpoint is the only thing that produces admin documents
// now, and every asset flows through the static-web-assets pipeline (_content/*,
// /_framework/*). What remains here is the request *gatekeeping* that has to happen
// before endpoint routing:
//
//   1. theme check - the Blazor admin shell only applies when the tenant's selected
//      admin theme is (or is tagged as) the Blazor one;
//   2. canonical-casing redirect - Blazor's NavigationManager compares the browser
//      URL against <base href> ordinally, so "/login" must 302 to "/Login";
//   3. authentication + per-route authorization for admin Blazor pages, server-side,
//      ahead of any rendering;
//   4. the path rewrite that bridges Orchard's tenant-configured admin prefix to
//      MapRazorComponents' compile-time route table: "/Admin/Features" becomes
//      "/Features" (the @page literal), "/Login" becomes "/login", and admin URLs
//      with no Crest Blazor page become "/legacy-host" (LegacyHost.razor, which
//      renders the LegacyAdminFrame the client-side Router's NotFound branch shows
//      for the same URLs). The matched shell base is stashed in HttpContext.Items
//      (CrestBlazorHosting) for the theme-dispatching App root to build <base href>.
// Inserts BlazorAdminThemeMiddleware ahead of the tenant pipeline's UseRouting() -
// OrchardCore applies IStartupFilters before it adds routing (ShellPipelineExtensions),
// while module Configure() middlewares all land after, where a Request.Path rewrite
// can no longer influence which endpoint was matched. See the registration comment in
// Startup.ConfigureServices.
internal sealed class BlazorAdminThemeStartupFilter : Microsoft.AspNetCore.Hosting.IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) =>
        app =>
        {
            app.UseMiddleware<BlazorAdminThemeMiddleware>();
            next(app);
        };
}

public sealed class BlazorAdminThemeMiddleware
{
    private const string LegacyHostRoute = "/legacy-host";

    private static readonly PathString CrestAdminThemePreviewPath = new("/OrchardCore.Crest.Admin/Theme.png");
    private const string CrestAdminThemePreviewAsset = "/_content/OrchardCore.Crest.Admin.Client/Theme.png";

    private readonly RequestDelegate _next;
    private readonly IOptions<BlazorAdminThemeOptions> _options;
    private readonly ILogger<BlazorAdminThemeMiddleware> _logger;

    public BlazorAdminThemeMiddleware(
        RequestDelegate next,
        IOptions<BlazorAdminThemeOptions> options,
        ILogger<BlazorAdminThemeMiddleware> logger)
    {
        _next = next;
        _options = options;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (LegacyFrameThemeSelector.IsLegacyFrameRequest(context))
        {
            await _next(context);
            return;
        }

        var requestPath = context.Request.Path;
        var options = _options.Value;
        var adminPath = new PathString(options.AdminPath);

        // Orchard's theme-gallery preview thumbnail. The wasm project is a Razor class
        // library now, so its wwwroot (including Theme.png) is a static web asset
        // under _content/ - redirect rather than resurrecting a file-serving path here.
        if (requestPath.Equals(CrestAdminThemePreviewPath))
        {
            context.Response.Redirect(CrestAdminThemePreviewAsset);
            return;
        }

        // blazor.web.js resolves its own infrastructure URLs against the document's
        // <base href> (the shell base this middleware stamps), not the site root - so
        // the browser asks for "/Login/_framework/dotnet.js", "/Admin/_blazor" (the
        // server-circuit hub), "/Admin/_content/..." etc. Those are all mapped at the
        // site root by MapRazorComponents/MapStaticAssets; strip the shell prefix and
        // pass through. This must run before the page gating below: "/Admin/_blazor"
        // has no file extension and would otherwise be treated as a page URL and
        // rewritten to /legacy-host, killing the interactive circuit. No theme check
        // here - these requests only follow a document this middleware already
        // theme-gated, and a stray one merely 404s at root.
        if (TryStripShellPrefixForBlazorInfrastructure(requestPath, adminPath, new PathString(options.LoginPath), out var infrastructurePath))
        {
            context.Request.Path = infrastructurePath;
            try
            {
                await _next(context);
            }
            finally
            {
                context.Request.Path = requestPath;
            }
            return;
        }

        var isAdminRoute = requestPath.StartsWithSegments(adminPath, out var adminRemainder);
        // LoginPath is a shared auth entry point served by the Blazor shell regardless
        // of admin/front-end, matched directly against options.LoginPath rather than
        // through auto-discovered @page literals - Login.razor's "@page "/login"" is a
        // WASM-router-relative route name, not a server path, so a tenant that
        // customizes LoginPath (e.g. "/signin") would otherwise never match here and
        // would silently fall through to Orchard's own (unconfigured,
        // Blazor-theme-incompatible) login flow.
        var isLoginRoute = requestPath.Equals(options.LoginPath, StringComparison.OrdinalIgnoreCase);

        // Only page requests are gated/rewritten. Asset requests (anything with a file
        // extension) are none of this middleware's business anymore - admin assets are
        // root-absolute _content/* / _framework/* URLs served by the static-assets
        // pipeline, never admin-path-prefixed.
        if ((!isAdminRoute && !isLoginRoute) || !IsPageRequest(requestPath))
        {
            await _next(context);
            return;
        }

        if (!await IsBlazorAdminThemeAsync(context, requestPath))
        {
            await _next(context);
            return;
        }

        // Route matching above is deliberately case-insensitive, but Blazor's
        // NavigationManager compares the browser URL against <base href> ordinally -
        // rendering the shell for "/login" with a <base href> of "/Login/" boots the
        // runtime and then throws "The URI ... is not contained by the base URI ...",
        // leaving a dead page. Canonicalize the matched prefix's casing with a
        // redirect instead.
        var canonicalPath = isLoginRoute
            ? options.LoginPath
            : options.AdminPath + adminRemainder.Value;
        if (!string.Equals(requestPath.Value, canonicalPath, StringComparison.Ordinal))
        {
            context.Response.Redirect(canonicalPath + context.Request.QueryString);
            return;
        }

        var isBlazorPageRoute = isLoginRoute
            || (isAdminRoute && await IsBlazorRouteAsync(context, adminRemainder, context.Request.Query));

        // Direct URL requests are authorized on the server. In-app navigation
        // uses the login manifest's batch as a fast UI guard, but that browser
        // state is deliberately never trusted as an authorization decision.
        if (isBlazorPageRoute && isAdminRoute)
        {
            // Crest gates the admin shell before Orchard's later authentication
            // middleware. Authenticate the same Orchard application cookie here
            // before making an early route decision.
            var authentication = await context.AuthenticateAsync(IdentityConstants.ApplicationScheme);
            if (authentication.Succeeded && authentication.Principal is not null)
            {
                context.User = authentication.Principal;
            }

            if (context.User.Identity?.IsAuthenticated != true)
            {
                context.Response.Redirect(options.LoginPath);
                return;
            }

            // CrestRoutePermissionProvider's templates are canonical ("/Features",
            // "/Themes", ... matching the WASM app's own @page directives, which
            // carry no prefix themselves), not real server paths - so matching must
            // happen against the canonical form of this request, not its real,
            // tenant-configured requestPath (e.g. "/backoffice/Features" would never
            // match "/Features" otherwise). adminRemainder is already exactly that
            // canonical form.
            var routeAuthorization = context.RequestServices.GetRequiredService<CrestRouteAuthorizationService>();
            if (!await routeAuthorization.CanAccessAsync(context.User, adminRemainder.Value))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
            }
        }

        // Bridge the tenant-configured prefix to MapRazorComponents' route table: the
        // endpoint routing that renders the App document matches raw server paths
        // against compile-time @page literals, which carry no admin prefix. The
        // browser URL is untouched (this is a server-internal rewrite) - client-side,
        // Blazor's Router resolves the original URL against the <base href> the App
        // root emits from the stashed shell base, landing on the same page.
        context.Items[CrestBlazorHosting.OriginalPathItem] = requestPath.Value;
        context.Items[CrestBlazorHosting.ShellBasePathItem] = isLoginRoute ? options.LoginPath : options.AdminPath;
        var rewrittenPath = isLoginRoute
            ? new PathString("/login")
            : isBlazorPageRoute
                ? (adminRemainder.HasValue ? adminRemainder : new PathString("/"))
                : new PathString(LegacyHostRoute);
        context.Request.Path = rewrittenPath;
        try
        {
            await _next(context);
        }
        finally
        {
            context.Request.Path = requestPath;
        }
    }

    private static bool TryStripShellPrefixForBlazorInfrastructure(
        PathString requestPath,
        PathString adminPath,
        PathString loginPath,
        out PathString infrastructurePath)
    {
        if ((requestPath.StartsWithSegments(adminPath, out var remainder) ||
             requestPath.StartsWithSegments(loginPath, out remainder)) &&
            (remainder.StartsWithSegments("/_framework") ||
             remainder.StartsWithSegments("/_content") ||
             remainder.StartsWithSegments("/_blazor")))
        {
            infrastructurePath = remainder;
            return true;
        }

        infrastructurePath = PathString.Empty;
        return false;
    }

    private static bool IsPageRequest(PathString requestPath)
    {
        var value = requestPath.Value;
        return string.IsNullOrEmpty(value) || !Path.HasExtension(value);
    }

    // adminRemainder is requestPath with the matched, real AdminPath prefix already
    // stripped (e.g. "/backoffice/Features" -> "/Features") - the same canonical shape
    // RouteComponentTable's entries are in, since @page directives themselves carry no
    // prefix. Comparing anything here against the real, absolute request path directly
    // would never match. Route discovery itself now lives entirely in
    // Crest.Routing.AdminRouteComponentTableProvider (reflection over [Route] attributes,
    // reusing the exact same table Crest.Routing.RouteGateMatcherPolicy consults) - this
    // middleware no longer scans .razor source files itself, avoiding two independent
    // "is this an Admin route" implementations drifting out of sync.
    private static async Task<bool> IsBlazorRouteAsync(HttpContext context, PathString adminRemainder, IQueryCollection query)
    {
        var normalized = NormalizeRoute(adminRemainder.Value);
        if (string.Equals(normalized, "/settings", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(query["groupId"], "SecurityHeaders", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var tableManager = context.RequestServices.GetRequiredService<Crest.Routing.IRouteComponentTableManager>();
        var table = await tableManager.GetRouteComponentTableAsync();
        return table.TryMatch(new PathString(normalized), out _);
    }

    private static string NormalizeRoute(string? route)
    {
        if (string.IsNullOrWhiteSpace(route) || route == "/")
        {
            return "/";
        }

        return "/" + route.Trim('/').ToLowerInvariant();
    }

    // Delegates the actual "is Blazor the active admin theme" question to
    // Crest.Routing.IBlazorAdminThemeDetector (the single source of truth
    // RouteGateMatcherPolicy and the route-component table also consult) - this method's
    // own job is purely "how do I get a DI scope to ask that question in", since this
    // middleware alone can run before the tenant's own request scope carries
    // IAdminThemeService (e.g. very early pipeline positions / cross-tenant probing).
    private async Task<bool> IsBlazorAdminThemeAsync(HttpContext context, PathString requestPath)
    {
        var detector = context.RequestServices.GetService<Crest.Routing.IBlazorAdminThemeDetector>();
        if (detector is not null)
        {
            return await detector.IsBlazorAdminThemeActiveAsync();
        }

        var shellHost = context.RequestServices.GetService<IShellHost>();
        if (shellHost is null)
        {
            _logger.LogDebug("Blazor admin route check for {Path}: no shell host is available.", requestPath);
            return false;
        }

        await shellHost.InitializeAsync();

        if (!shellHost.TryGetSettings("Default", out var shellSettings))
        {
            _logger.LogDebug("Blazor admin route check for {Path}: Default shell settings are not available.", requestPath);
            return false;
        }

        var isBlazorAdminTheme = false;
        await (await shellHost.GetScopeAsync(shellSettings)).UsingServiceScopeAsync(async scope =>
        {
            var scopedDetector = scope.ServiceProvider.GetRequiredService<Crest.Routing.IBlazorAdminThemeDetector>();
            isBlazorAdminTheme = await scopedDetector.IsBlazorAdminThemeActiveAsync();
        });

        return isBlazorAdminTheme;
    }
}
