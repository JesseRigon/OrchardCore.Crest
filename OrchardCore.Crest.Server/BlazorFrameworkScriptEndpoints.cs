using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;

namespace Crest;

// The Blazor framework boot scripts (blazor.web.js and friends) ship via the
// microsoft.aspnetcore.app.internal.assets SDK package's static-web-assets target,
// but that target only fires when OutputType=Exe AND UsingMicrosoftNETSdkWeb=true -
// neither is true for OrchardCore.Crest.csproj, a Sdk="Microsoft.NET.Sdk.Razor"
// module library (Orchard's module convention), so the scripts never reach the app's
// static web assets manifest and MapStaticAssets cannot serve them. See
// docs/BlazorWeb.md (Bug 2).
//
// Served here as tenant-pipeline ENDPOINTS (not a host-level UseStaticFiles) so that
// OrchardCore's routing stays the single authority over the URL space: endpoint
// matching happens after ModularTenantRouterMiddleware has stripped the tenant's
// RequestUrlPrefix into PathBase and after BlazorAdminThemeMiddleware has stripped
// the admin/login shell base the same way - so one registration serves the script
// for every form a browser can request it in ("/_framework/blazor.web.js",
// "/tenant2/_framework/...", "/tenant2/Admin/_framework/..."). A host-level
// UseStaticFiles registered before UseOrchardCore() (the previous approach, in each
// consuming host's Program.cs) only ever matched the bare root form and had to be
// copy-pasted into every host.
//
// MapStaticAssets' own endpoints are exact literal routes built from the manifest;
// these scripts are absent from that manifest by construction, so the parameterized
// template below can never shadow a manifest asset - literals always win over
// route parameters in endpoint routing.
internal static class BlazorFrameworkScriptEndpoints
{
    // The full contents of the package's _framework directory - serve exactly these,
    // nothing else, so this can never become a general file-serving side door.
    private static readonly Dictionary<string, string> KnownScripts = new(StringComparer.OrdinalIgnoreCase)
    {
        ["blazor.web.js"] = "text/javascript",
        ["blazor.web.js.map"] = "application/json",
        ["blazor.server.js"] = "text/javascript",
        ["blazor.server.js.map"] = "application/json",
        ["blazor.webassembly.js"] = "text/javascript",
        ["blazor.webassembly.js.map"] = "application/json",
    };

    public static void MapBlazorFrameworkScripts(this IEndpointRouteBuilder routes, ILogger logger)
    {
        var frameworkAssetsRoot = ResolveFrameworkAssetsRoot(logger);
        if (frameworkAssetsRoot is null)
        {
            logger.LogWarning(
                "The microsoft.aspnetcore.app.internal.assets package directory was not found under the NuGet " +
                "package root; the Blazor framework scripts (blazor.web.js) will 404 and interactive render " +
                "modes will not boot.");
            return;
        }

        routes.MapGet("/_framework/{fileName}", (string fileName, HttpContext context) =>
        {
            if (!KnownScripts.TryGetValue(fileName, out var contentType))
            {
                return Results.NotFound();
            }

            var filePath = Path.Combine(frameworkAssetsRoot, fileName);
            if (!File.Exists(filePath))
            {
                return Results.NotFound();
            }

            // The script is served at a stable (unfingerprinted) URL, so it must
            // revalidate rather than cache immutably - LastModified gives PhysicalFile
            // conditional-request (304) handling for free.
            context.Response.Headers.CacheControl = "no-cache";
            return Results.File(filePath, contentType, lastModified: File.GetLastWriteTimeUtc(filePath));
        });
    }

    private static string? ResolveFrameworkAssetsRoot(ILogger logger)
    {
        var packageRoot = Path.Combine(
            Environment.GetEnvironmentVariable("NUGET_PACKAGES")
                ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages"),
            "microsoft.aspnetcore.app.internal.assets");

        if (!Directory.Exists(packageRoot))
        {
            return null;
        }

        // Prefer the package version matching the running shared framework, so the
        // boot script and the runtime it boots can never drift apart (the package
        // version tracks Microsoft.AspNetCore.App's own). Environment.Version is the
        // runtime version (e.g. 10.0.9), which matches in every normal install.
        var runtimeVersionDir = Path.Combine(packageRoot, Environment.Version.ToString(3), "_framework");
        if (Directory.Exists(runtimeVersionDir))
        {
            return runtimeVersionDir;
        }

        var fallback = Directory
            .EnumerateDirectories(packageRoot)
            .OrderByDescending(path => path)
            .Select(path => Path.Combine(path, "_framework"))
            .FirstOrDefault(Directory.Exists);

        if (fallback is not null)
        {
            logger.LogWarning(
                "No microsoft.aspnetcore.app.internal.assets package matches the running framework version " +
                "{RuntimeVersion}; serving Blazor framework scripts from {Fallback} instead. A version mismatch " +
                "between blazor.web.js and the runtime can cause silent boot failures.",
                Environment.Version.ToString(3), fallback);
        }

        return fallback;
    }
}
