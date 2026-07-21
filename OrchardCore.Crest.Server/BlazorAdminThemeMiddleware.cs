using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.Environment.Extensions;
using OrchardCore.Environment.Shell;
using OrchardCore.Environment.Shell.Scope;

namespace Crest;

public sealed class BlazorAdminThemeOptions
{
    public string AdminPath { get; set; } = "/admin";
    public string BlazorThemeTag { get; set; } = "blazor";
    public string BlazorAdminThemeId { get; set; } = "OrchardCore.Crest.Admin";
    public string AdminThemeSourceWebRoot { get; set; } = "modules/OrchardCore.Crest/OrchardCore.Crest.Admin/wasm/wwwroot";
    public string AdminThemeBuildWebRoot { get; set; } = "modules/OrchardCore.Crest/OrchardCore.Crest.Admin/wasm/bin/OrchardCore.Crest.Admin/Debug/net10.0/wwwroot";
    public HashSet<string> BlazorRoutes { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        "/admin",
        "/admin/themes",
        "/admin/menus",
        "/admin/menu",
        "/admin/menu/list",
        "/admin/contents/contentitems/menu",
        "/admin/settings/admin",
        "/admin/settings/general",
    };

    public string[] BlazorRouteSourceDirectories { get; set; } =
    [
        "modules/OrchardCore.Crest/OrchardCore.Crest.Admin/wasm/Pages",
    ];
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
        var isBlazorAssetRoute = IsBlazorAssetRoute(requestPath);
        var isBlazorPageRoute = IsPageRequest(requestPath) && IsBlazorRoute(options, requestPath);
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

        if (webRoots.Length == 0)
        {
            _logger.LogWarning("Blazor admin theme web roots were not found. Letting Orchard handle {Path}.", requestPath);
            await _next(context);
            return;
        }

        var relativePath = isAdminRoute
            ? GetAdminRelativePath(adminRemainder)
            : requestPath.Value?.TrimStart('/') ?? string.Empty;

        if (string.IsNullOrWhiteSpace(relativePath) || !Path.HasExtension(relativePath))
        {
            relativePath = "index.html";
        }

        foreach (var webRoot in webRoots)
        {
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

    private bool IsBlazorRoute(BlazorAdminThemeOptions options, PathString requestPath)
    {
        var normalized = NormalizeRoute(requestPath.Value);
        return ResolveBlazorRoutes(options).Contains(normalized);
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

            var routes = new HashSet<string>(options.BlazorRoutes.Select(NormalizeRoute), StringComparer.OrdinalIgnoreCase);

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
        if (relativePath.StartsWith("_content/Radzen.Blazor/", StringComparison.OrdinalIgnoreCase))
        {
            var packageRoot = ResolveNuGetPackageStaticWebAssets("radzen.blazor");
            if (packageRoot is not null && await TryServeFileAsync(context, packageRoot, relativePath["_content/Radzen.Blazor/".Length..]))
            {
                return true;
            }
        }

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

    private static string? ResolveNuGetPackageStaticWebAssets(string packageId)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var packageRoot = Path.Combine(home, ".nuget", "packages", packageId);
        if (!Directory.Exists(packageRoot))
        {
            return null;
        }

        return Directory.GetDirectories(packageRoot)
            .Select(directory => new { Directory = directory, Version = ParseVersion(Path.GetFileName(directory)) })
            .OrderByDescending(candidate => candidate.Version)
            .Select(candidate => Path.Combine(candidate.Directory, "staticwebassets"))
            .FirstOrDefault(Directory.Exists);
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
