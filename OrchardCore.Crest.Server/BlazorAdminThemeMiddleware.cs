using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Crest.Services;
using OrchardCore.Admin;
using OrchardCore.Environment.Extensions;
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
    public string BlazorThemeTag { get; set; } = "blazor";
    public string BlazorAdminThemeId { get; set; } = "OrchardCore.Crest.Admin";
    public string AdminThemeSourceWebRoot { get; set; } = "modules/OrchardCore.Crest/OrchardCore.Crest.Admin/wasm/wwwroot";
    public string AdminThemeBuildWebRoot { get; set; } = "modules/OrchardCore.Crest/OrchardCore.Crest.Admin/wasm/bin/OrchardCore.Crest.Admin/Debug/net10.0/wwwroot";

    // Crest-only route overrides: paths with no Orchard equivalent that still need to
    // be served by the Blazor admin shell. Everything that DOES have a real .razor
    // page under BlazorRouteSourceDirectories is discovered automatically
    // (DiscoverRazorPageRoutes) and does not need to be listed here - listing it
    // anyway would be redundant and risks drifting out of sync with the actual @page
    // declarations, which is exactly how the tenant front-end root "/" bug happened.
    public HashSet<string> BlazorRouteOverrides { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public string[] BlazorRouteSourceDirectories { get; set; } =
    [
        "modules/OrchardCore.Crest/OrchardCore.Crest.Admin/wasm/Pages",
    ];
}

// Keeps BlazorAdminThemeOptions.AdminPath/LoginPath in sync with Orchard's own,
// real, tenant-configurable settings (AdminOptions.AdminUrlPrefix,
// UserOptions.LoginPath - both bound from shell config, e.g. a recipe's
// "OrchardCore_Admin"/"OrchardCore_Users" sections) instead of Crest hardcoding its
// own copies that silently drift if a tenant customizes either prefix. Runs as
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
    }
}

public sealed class BlazorAdminThemeMiddleware
{
    private static readonly PathString FrameworkPath = new("/_framework");
    private static readonly PathString ContentPath = new("/_content");
    private static readonly PathString CrestAdminThemePreviewPath = new("/OrchardCore.Crest.Admin/Theme.png");

    private readonly RequestDelegate _next;
    private readonly IHostEnvironment _environment;
    private readonly IOptions<BlazorAdminThemeOptions> _options;
    private readonly FileExtensionContentTypeProvider _contentTypes = new();
    private readonly ILogger<BlazorAdminThemeMiddleware> _logger;
    private readonly object _blazorRoutesLock = new();
    private HashSet<string>? _blazorRoutes;

    public BlazorAdminThemeMiddleware(
        RequestDelegate next,
        IHostEnvironment environment,
        IOptions<BlazorAdminThemeOptions> options,
        ILogger<BlazorAdminThemeMiddleware> logger)
    {
        _next = next;
        _environment = environment;
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

        var isAdminRoute = requestPath.StartsWithSegments(adminPath, out var adminRemainder);
        // LoginPath is a shared auth entry point served by the Blazor shell regardless
        // of admin/front-end (matching IsBlazorRoute's own admin-settings special case
        // below), and unlike AdminPath's own routes it's matched directly against
        // options.LoginPath rather than through auto-discovered @page literals -
        // Login.razor's "@page "/login"" is a WASM-router-relative route name, not a
        // server path, so a tenant that customizes LoginPath (e.g. "/signin") would
        // otherwise never match here and would silently fall through to Orchard's own
        // (unconfigured, Blazor-theme-incompatible) login flow.
        var isLoginRoute = requestPath.Equals(options.LoginPath, StringComparison.OrdinalIgnoreCase);
        // The admin shell's assets are only ever served under the tenant-configured
        // AdminPath/LoginPath prefixes - index.html references them base-relative, so
        // they arrive as "{shellBasePath}/_framework/..." etc., and the matched prefix
        // is stripped before resolving against the admin theme's web roots. Root
        // "/_framework/*" (and every other unprefixed path) is deliberately NOT
        // intercepted: that URL space belongs to Crest.Server's Blazor Web App host
        // (Site's WASM client via MapStaticAssets), and both apps ship
        // identically-named framework files (dotnet.js, blazor.webassembly.js, the
        // runtime scripts) whose .NET 10 embedded boot config decides which app boots -
        // serving the wrong one leaves the shell at "Loading..." forever with no error.
        var assetRequestPath = requestPath;
        var isBlazorAssetRoute = false;
        if (requestPath.StartsWithSegments(adminPath, out var adminAssetRemainder))
        {
            assetRequestPath = adminAssetRemainder;
            isBlazorAssetRoute = IsBlazorAssetRoute(adminAssetRemainder);
        }
        else if (requestPath.StartsWithSegments(new PathString(options.LoginPath), out var loginAssetRemainder))
        {
            assetRequestPath = loginAssetRemainder;
            isBlazorAssetRoute = IsBlazorAssetRoute(loginAssetRemainder);
        }
        // Blazor page routes are discovered from wasm/Pages/*.razor's own @page
        // directives (see DiscoverRazorPageRoutes below), which are WASM-router-relative
        // route names, not server-side paths - they only tell us which segments under
        // AdminPath correspond to a real page (as opposed to an unknown/404 path).
        // isAdminRoute is the actual server-side gate: without it, an auto-discovered
        // literal like "/Features" would keep matching even after a tenant changes
        // AdminUrlPrefix away from the stock default, since discovery has no idea
        // what the *current* AdminPath is. OrchardCore.Crest.Admin/wasm/
        // Pages/Home.razor declares "@page "/"" - that's the admin app's own internal
        // landing route (served when requestPath resolves to isAdminRoute's own root),
        // not the tenant's front-end root, and must never be treated as one: the
        // Blazor admin theme owns AdminPath only, everything else belongs to the site
        // theme (OrchardCore.Crest.Site must keep working standalone, without
        // OrchardCore.Crest.Admin, for tenants using the standard Orchard admin theme).
        var isBlazorPageRoute = isLoginRoute
            || (isAdminRoute
                && IsPageRequest(requestPath)
                && IsBlazorRoute(options, adminRemainder, context.Request.Query));
        var isCrestAdminThemePreviewRoute = requestPath.Equals(CrestAdminThemePreviewPath);

        if (!isAdminRoute && !isBlazorAssetRoute && !isBlazorPageRoute && !isCrestAdminThemePreviewRoute)
        {
            await _next(context);
            return;
        }

        var webRoots = ResolveAdminThemeWebRoots(options).ToArray();
        if (isCrestAdminThemePreviewRoute)
        {
            foreach (var webRoot in webRoots)
            {
                if (await TryServeFileAsync(context, webRoot, "Theme.png"))
                {
                    return;
                }
            }

            await _next(context);
            return;
        }

        if (!await IsBlazorAdminThemeAsync(context, options, requestPath))
        {
            await _next(context);
            return;
        }

        // Route matching above is deliberately case-insensitive, but Blazor's
        // NavigationManager compares the browser URL against <base href> ordinally -
        // serving the shell for "/login" with a rewritten base of "/Login/" boots the
        // runtime and then throws "The URI ... is not contained by the base URI ...",
        // leaving the page stuck on the index.html "Loading..." placeholder.
        // Canonicalize the matched prefix's casing with a redirect instead.
        if (isBlazorPageRoute)
        {
            var canonicalPath = isLoginRoute
                ? options.LoginPath
                : options.AdminPath + adminRemainder.Value;
            if (!string.Equals(requestPath.Value, canonicalPath, StringComparison.Ordinal))
            {
                context.Response.Redirect(canonicalPath + context.Request.QueryString);
                return;
            }
        }

        // Direct URL requests are authorized on the server. In-app navigation
        // uses the login manifest's batch as a fast UI guard, but that browser
        // state is deliberately never trusted as an authorization decision.
        if (isBlazorPageRoute && isAdminRoute)
        {
            // Crest serves the WASM shell before Orchard's later authentication
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
            // canonical form: it's requestPath with only the matched AdminPath
            // prefix stripped, e.g. "/backoffice/Features" -> "/Features" - the same
            // shape Blazor's Router itself resolves "@page "/Features"" to, relative
            // to BaseUri (AdminPath, per TryServeIndexHtmlAsync).
            var routeAuthorization = context.RequestServices.GetRequiredService<CrestRouteAuthorizationService>();
            if (!await routeAuthorization.CanAccessAsync(context.User, adminRemainder.Value))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
            }
        }

        if (webRoots.Length == 0)
        {
            _logger.LogWarning("Blazor admin theme web roots were not found. Letting Orchard handle {Path}.", requestPath);
            await _next(context);
            return;
        }

        var relativePath = isAdminRoute
            ? GetAdminRelativePath(adminRemainder)
            : assetRequestPath.Value?.TrimStart('/') ?? string.Empty;

        if (string.IsNullOrWhiteSpace(relativePath) || !Path.HasExtension(relativePath))
        {
            relativePath = "index.html";
        }

        // The WASM shell can be reached at two independent server paths - AdminPath
        // and LoginPath - which are not necessarily nested (e.g. "/backoffice" and
        // "/signin"). <base href> must match whichever one actually served this
        // request, or Blazor's Router (which resolves @page routes relative to
        // BaseUri) won't find a match. isLoginRoute is checked first because a
        // custom LoginPath could theoretically overlap-prefix AdminPath's own
        // segments; an exact-match login request always means "serve the login base".
        var shellBasePath = isLoginRoute ? options.LoginPath : options.AdminPath;

        foreach (var webRoot in webRoots)
        {
            if (string.Equals(relativePath, "index.html", StringComparison.OrdinalIgnoreCase))
            {
                if (await TryServeIndexHtmlAsync(context, webRoot, shellBasePath))
                {
                    return;
                }

                continue;
            }

            if (await TryServeFileAsync(context, webRoot, relativePath))
            {
                return;
            }
        }

        if (await TryServeStaticWebAssetFallbackAsync(context, relativePath))
        {
            return;
        }

        await _next(context);
    }

    private static bool IsBlazorAssetRoute(PathString requestPath)
    {
        if (requestPath.StartsWithSegments(FrameworkPath) || requestPath.StartsWithSegments(ContentPath))
        {
            return true;
        }

        var value = requestPath.Value;
        return !string.IsNullOrWhiteSpace(value) && Path.HasExtension(value);
    }

    private static string GetAdminRelativePath(PathString adminRemainder)
    {
        var value = adminRemainder.Value?.TrimStart('/') ?? string.Empty;
        return string.IsNullOrEmpty(value) ? "index.html" : value;
    }

    private static bool IsPageRequest(PathString requestPath)
    {
        var value = requestPath.Value;
        return string.IsNullOrEmpty(value) || !Path.HasExtension(value);
    }

    // adminRemainder is requestPath with the matched, real AdminPath prefix already
    // stripped (e.g. "/backoffice/Features" -> "/Features") - the same canonical
    // shape ResolveBlazorRoutes' auto-discovered @page routes are in, since those
    // directives themselves carry no prefix. Comparing anything here against the
    // real, absolute request path directly would never match.
    private bool IsBlazorRoute(BlazorAdminThemeOptions options, PathString adminRemainder, IQueryCollection query)
    {
        var normalized = NormalizeRoute(adminRemainder.Value);
        if (string.Equals(normalized, "/settings", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(query["groupId"], "SecurityHeaders", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return ResolveBlazorRoutes(options).Any(route => CrestRouteAuthorizationService.Matches(route, normalized));
    }

    private HashSet<string> ResolveBlazorRoutes(BlazorAdminThemeOptions options)
    {
        if (_blazorRoutes is not null)
        {
            return _blazorRoutes;
        }

        lock (_blazorRoutesLock)
        {
            if (_blazorRoutes is not null)
            {
                return _blazorRoutes;
            }

            var routes = new HashSet<string>(options.BlazorRouteOverrides.Select(NormalizeRoute), StringComparer.OrdinalIgnoreCase);

            foreach (var sourceDirectory in ResolveBlazorRouteSourceDirectories(options))
            {
                foreach (var route in DiscoverRazorPageRoutes(sourceDirectory))
                {
                    routes.Add(route);
                }
            }

            _logger.LogInformation("Blazor admin routes: {Routes}", string.Join(", ", routes.Order(StringComparer.OrdinalIgnoreCase)));
            _blazorRoutes = routes;
            return routes;
        }
    }

    private IEnumerable<string> ResolveBlazorRouteSourceDirectories(BlazorAdminThemeOptions options)
    {
        foreach (var routeSource in options.BlazorRouteSourceDirectories)
        {
            yield return Path.Combine(_environment.ContentRootPath, routeSource);
            yield return Path.Combine(AppContext.BaseDirectory, routeSource);
        }
    }

    private static IEnumerable<string> DiscoverRazorPageRoutes(string sourceDirectory)
    {
        if (!Directory.Exists(sourceDirectory))
        {
            yield break;
        }

        foreach (var file in Directory.EnumerateFiles(sourceDirectory, "*.razor", SearchOption.AllDirectories))
        {
            foreach (var line in File.ReadLines(file))
            {
                var trimmed = line.Trim();
                if (!trimmed.StartsWith("@page", StringComparison.Ordinal))
                {
                    continue;
                }

                var firstQuote = trimmed.IndexOf('"');
                var lastQuote = trimmed.LastIndexOf('"');
                if (firstQuote < 0 || lastQuote <= firstQuote)
                {
                    continue;
                }

                yield return NormalizeRoute(trimmed[(firstQuote + 1)..lastQuote]);
            }
        }
    }

    private static string NormalizeRoute(string? route)
    {
        if (string.IsNullOrWhiteSpace(route) || route == "/")
        {
            return "/";
        }

        return "/" + route.Trim('/').ToLowerInvariant();
    }

    private async Task<bool> IsBlazorAdminThemeAsync(HttpContext context, BlazorAdminThemeOptions options, PathString requestPath)
    {
        var adminThemeService = context.RequestServices.GetService<IAdminThemeService>();
        if (adminThemeService is not null)
        {
            return await IsBlazorAdminThemeAsync(adminThemeService, options, context, requestPath);
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
            var scopedAdminThemeService = scope.ServiceProvider.GetRequiredService<IAdminThemeService>();
            isBlazorAdminTheme = await IsBlazorAdminThemeAsync(scopedAdminThemeService, options, context, requestPath);
        });

        return isBlazorAdminTheme;
    }

    private async Task<bool> IsBlazorAdminThemeAsync(IAdminThemeService adminThemeService, BlazorAdminThemeOptions options, HttpContext context, PathString requestPath)
    {
        var adminThemeName = await adminThemeService.GetAdminThemeNameAsync();
        var adminTheme = await adminThemeService.GetAdminThemeAsync();
        var hasBlazorTag = HasBlazorTag(adminTheme, options.BlazorThemeTag);
        var isBlazorAdminTheme = string.Equals(adminThemeName, options.BlazorAdminThemeId, StringComparison.OrdinalIgnoreCase) || hasBlazorTag;

        _logger.LogDebug(
            "Blazor admin route check for {Path}: selected admin theme name '{AdminThemeName}', resolved extension '{ExtensionId}', has '{Tag}' tag: {HasBlazorTag}, serving Blazor: {ServeBlazor}.",
            requestPath,
            adminThemeName,
            adminTheme?.Id,
            options.BlazorThemeTag,
            hasBlazorTag,
            isBlazorAdminTheme);

        return isBlazorAdminTheme;
    }

    private static bool HasBlazorTag(IExtensionInfo? extension, string tag)
    {
        return extension?.Manifest?.Tags?.Any(candidate => string.Equals(candidate, tag, StringComparison.OrdinalIgnoreCase)) == true;
    }

    private IEnumerable<string> ResolveAdminThemeWebRoots(BlazorAdminThemeOptions options)
    {
        var candidates = new[]
        {
            Path.Combine(_environment.ContentRootPath, options.AdminThemeBuildWebRoot),
            Path.Combine(_environment.ContentRootPath, options.AdminThemeSourceWebRoot),
            Path.Combine(AppContext.BaseDirectory, options.AdminThemeBuildWebRoot),
            Path.Combine(AppContext.BaseDirectory, options.AdminThemeSourceWebRoot),
        };

        return candidates.Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private async Task<bool> TryServeStaticWebAssetFallbackAsync(HttpContext context, string relativePath)
    {
        if (relativePath.StartsWith("_framework/Microsoft.DotNet.HotReload.WebAssembly.Browser.", StringComparison.OrdinalIgnoreCase)
            && relativePath.EndsWith(".lib.module.js", StringComparison.OrdinalIgnoreCase))
        {
            var sdkAsset = ResolveDotNetSdkWebAssemblyAsset("Microsoft.DotNet.HotReload.WebAssembly.Browser.lib.module.js");
            if (sdkAsset is not null)
            {
                return await TryServeFileAsync(context, Path.GetDirectoryName(sdkAsset)!, Path.GetFileName(sdkAsset));
            }
        }

        return false;
    }

    private static string? ResolveDotNetSdkWebAssemblyAsset(string fileName)
    {
        var sdkRoot = Path.Combine(Path.GetPathRoot(AppContext.BaseDirectory) ?? "/", "usr", "share", "dotnet", "sdk");
        if (!Directory.Exists(sdkRoot))
        {
            sdkRoot = "/usr/share/dotnet/sdk";
        }

        return Directory.Exists(sdkRoot)
            ? Directory.GetDirectories(sdkRoot)
                .Select(directory => new { Directory = directory, Version = ParseVersion(Path.GetFileName(directory)) })
                .OrderByDescending(candidate => candidate.Version)
                .Select(candidate => Path.Combine(candidate.Directory, "Sdks", "Microsoft.NET.Sdk.WebAssembly", "tools", "net10.0", "wwwroot", fileName))
                .FirstOrDefault(File.Exists)
            : null;
    }

    private static Version ParseVersion(string? value)
        => Version.TryParse(value, out var version) ? version : new Version(0, 0);

    // The WASM app's Router, @page directives, and base-relative NavigateTo calls
    // all use canonical paths carrying no prefix ("/Features", "/login") - it's
    // <base href>, rewritten below, that lets those literals resolve correctly
    // under any tenant-configured prefix (Blazor's Router matches @page routes
    // relative to NavigationManager.BaseUri). CrestRoutingController separately
    // gives the WASM app the real AdminPath/LoginPath values themselves, for
    // building cross-shell (absolute, forceLoad) navigation targets.
    private async Task<bool> TryServeIndexHtmlAsync(HttpContext context, string webRoot, string shellBasePath)
    {
        var provider = new PhysicalFileProvider(webRoot);
        var file = provider.GetFileInfo("index.html");
        if (!file.Exists || file.IsDirectory)
        {
            return false;
        }

        string html;
        await using (var stream = file.CreateReadStream())
        using (var reader = new StreamReader(stream))
        {
            html = await reader.ReadToEndAsync(context.RequestAborted);
        }

        // Blazor's Router matches @page routes relative to NavigationManager.BaseUri,
        // which WebAssemblyHostBuilder derives from this <base> tag at boot - not from
        // any literal leading "/" in each @page directive. Rewriting it here to
        // whichever real, configured path (AdminPath or LoginPath) actually served
        // this request is what lets all 30+ .razor pages under wasm/Pages, including
        // Login.razor's own "@page "/login"", keep their unmodified routes working
        // verbatim under any AdminUrlPrefix/LoginPath, without a NavigationManager
        // subclass (WebAssemblyNavigationManager's JS-interop wiring isn't designed
        // to be wrapped) or per-file edits.
        var trimmedBasePath = shellBasePath.Trim('/');
        var baseHref = string.IsNullOrEmpty(trimmedBasePath) ? "/" : "/" + trimmedBasePath + "/";
        html = html.Replace("<base href=\"/\" />", $"<base href=\"{System.Net.WebUtility.HtmlEncode(baseHref)}\" />", StringComparison.OrdinalIgnoreCase);

        context.Response.ContentType = "text/html";
        context.Response.Headers.CacheControl = "no-cache, no-store, must-revalidate";
        await context.Response.WriteAsync(html, context.RequestAborted);
        return true;
    }

    private async Task<bool> TryServeFileAsync(HttpContext context, string webRoot, string relativePath)
    {
        if (relativePath.Contains("..", StringComparison.Ordinal))
        {
            return false;
        }

        var provider = new PhysicalFileProvider(webRoot);
        var file = provider.GetFileInfo(relativePath.Replace('\\', '/'));
        if (!file.Exists || file.IsDirectory)
        {
            return false;
        }

        if (!_contentTypes.TryGetContentType(file.Name, out var contentType))
        {
            contentType = "application/octet-stream";
        }

        context.Response.ContentType = contentType;
        context.Response.ContentLength = file.Length;

        if (string.Equals(file.Name, "index.html", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.Headers.CacheControl = "no-cache, no-store, must-revalidate";
        }

        await using var stream = file.CreateReadStream();
        await stream.CopyToAsync(context.Response.Body, context.RequestAborted);
        return true;
    }
}
