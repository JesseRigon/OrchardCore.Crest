using Crest.Blazor;
using Crest.Blazor.Drivers;
using Crest.Blazor.Models;
using Crest.Components.Primitives;
using OrchardCore.Crest.Components;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Crest.Iconify;
using Crest.Icons;
using Crest.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.JSInterop;
using Crest.Blazor.Migrations;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.Data.Migration;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Theming;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Recipes;
using OrchardCore.Security;
using OrchardCore.Security.Permissions;

namespace Crest;

[Feature("OrchardCore.Crest")]
public sealed class Startup : StartupBase
{
    private const string CrestWebCors = "CrestWeb";

    public override int Order => -1000;

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCors(options =>
        {
            options.AddPolicy(CrestWebCors, policy => policy
                .WithOrigins("http://localhost:5011", "http://127.0.0.1:5011")
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials());
        });

        services.AddHttpContextAccessor();
        services.AddSignalR();
        services.AddScoped<ICrestRequestAccess, CrestRequestAccess>();
        services.AddScoped<ICrestRoutePermissionProvider, CrestRoutePermissionProvider>();
        services.AddScoped<CrestRouteAuthorizationService>();
        services.AddScoped<ICrestPermissionInvalidator, CrestPermissionInvalidator>();
        services.AddScoped<ICrestAdminMenuLayoutInvalidator, CrestAdminMenuLayoutInvalidator>();
        services.AddScoped<IRoleUpdatedEventHandler, CrestRolePermissionInvalidationHandler>();
        services.AddScoped<IThemeSelector, LegacyFrameThemeSelector>();
        services.AddScoped<CrestAdminMenuLayoutService>();
        services.AddScoped<CrestMenuPlacementService>();
        services.AddScoped<CrestProfileMenuService>();
        services.AddScoped<CrestPrimaryNavMenuSettingsStore>();
        services.AddScoped<CrestAdminSettingsNormalizer>();
        services.AddScoped<CrestTitleBarSettingsStore>();
        services.AddNavigationProvider<CrestAdminMenu>();
        services.AddScoped<IIconProvider, IconifyIconProvider>();
        services.AddScoped<IIconProviderSettingsStore, CrestIconProviderSettingsStore>();
        services.AddSingleton<IIconifyLocalMirrorPathProvider, CrestIconifyLocalMirrorPathProvider>();
        services.AddSingleton<IIconifyLocalMirrorStore, IconifyLocalMirrorStore>();
        services.AddHostedService<IconifyCacheRefreshService>();
        services.AddSingleton<SvgIconSanitizer>();
        services.AddHttpClient("OrchardCore.Crest.Icons.Iconify");
        services.AddScoped<IIconRegistry, CompositeIconRegistry>();
        services.AddScoped<CrestIconSourceStore>();
        services.AddScoped<CrestIconController>();
        services.AddRecipeExecutionStep<Recipes.CrestAdminMenuLayoutStep>();
        services.Configure<BlazorAdminThemeOptions>(options => { });
        services.AddTransient<IPostConfigureOptions<BlazorAdminThemeOptions>, BlazorAdminThemeOptionsConfiguration>();
        services.AddCrestCultureCookieProvider();

        // Crest.Server is the single Blazor Web App host for both API and SSR - the only
        // server needed for any Blazor-capable Crest theme (Site, Admin once Phase 8
        // converts it, or a future third-party theme). Registered unconditionally here,
        // exactly like BlazorAdminThemeMiddleware below: whether a tenant's *currently
        // selected* theme actually uses this hosting is a per-request runtime check
        // (mirroring IsBlazorAdminThemeAsync), not a [Feature] gate - a tenant could
        // have a Blazor theme installed but be running a different one, and that
        // decision can change without a feature enable/disable.
        //
        // Both AddInteractiveServerComponents() and AddInteractiveWebAssemblyComponents()
        // are required for InteractiveAuto (Server-first on the initial visit while the
        // WASM runtime downloads in the background, then WASM for every visit after -
        // "WASM as the primary route, Server as the fallback" per the plan doc's own
        // notes). Static-SSR-only content (the overwhelming majority - anything rendered
        // via CrestBlazorComponentShapeBindingResolver's HtmlRenderer path) never touches
        // either of these; they only matter for components explicitly marked
        // @rendermode, i.e. genuine interactive islands like Components/Pages/
        // BlazorCounter.razor. See plans/blazor hybrid conversion.md, Phase 3/3.5.
        services.AddRazorComponents()
            .AddInteractiveServerComponents()
            .AddInteractiveWebAssemblyComponents();

        // InteractiveAuto's first render runs server-side (SignalR circuit) before a
        // WASM runtime is even downloaded - any interactive-island component that calls
        // a relative api/crest/* endpoint via HttpClient (e.g. BlazorCounter.razor,
        // which can't inject IContentManager once it also has to run in WASM - see
        // docs/BlazorWeb.md) needs an HttpClient with a real BaseAddress during that
        // server-side phase too, mirroring what wasm/Program.cs sets up client-side.
        // Scoped + IHttpContextAccessor-derived base address: correct per-request even
        // behind a reverse proxy/different host header, and matches the WASM client's
        // own builder.HostEnvironment.BaseAddress semantics (the app's own origin).
        // (IHttpContextAccessor is already registered above.)
        services.AddScoped(sp =>
        {
            var httpContext = sp.GetRequiredService<IHttpContextAccessor>().HttpContext!;
            var baseAddress = $"{httpContext.Request.Scheme}://{httpContext.Request.Host}{httpContext.Request.PathBase}/";
            return new HttpClient { BaseAddress = new Uri(baseAddress) };
        });
        services.AddCrestComponents();
        services.AddCrestIconClient();

        // Phase 3: Blazor participates in Orchard's own shape pipeline instead of a
        // custom request-intercepting middleware - see plans/blazor hybrid
        // conversion.md's "Orchard's routing does not fit stock Blazor Web App hosting"
        // finding for why. CrestBlazorComponentPart is the tenant-placeable tree node
        // (mirrors WidgetsListPart/BagPart); the registry is the shared, tenant-agnostic
        // catalog of components tenants can place, scanned once from Server's own
        // Components/Icons references - the same assemblies already flow into
        // AddRazorComponents() above, so no separate discovery mechanism is needed here.
        // Registered as a singleton: the catalog is process-wide, not per-tenant (see
        // ICrestBlazorComponentRegistry's own comment).
        services.AddSingleton<ICrestBlazorComponentRegistry>(_ =>
            new AssemblyScanningCrestBlazorComponentRegistry(
            [
                typeof(Crest.Components.Primitives.ServiceCollectionExtensions).Assembly,
                typeof(IIconRegistry).Assembly,
            ]));
        services.AddScoped<IShapeBindingResolver, CrestBlazorComponentShapeBindingResolver>();
        services.AddScoped<IContentPartDisplayDriver, CrestBlazorComponentPartDisplayDriver>();
        services.AddContentPart<CrestBlazorComponentPart>();
        services.AddDataMigration<CrestBlazorComponentMigrations>();

        // HtmlRenderer (used by CrestBlazorComponentShapeBindingResolver for Static SSR)
        // instantiates components via this same service provider, and component base
        // classes routinely have [Inject] IJSRuntime - without any registration, DI throws
        // before rendering even starts, for components that never call JS during initial
        // render. TryAddScoped: only wins when nothing else already registered IJSRuntime
        // (interactive Server/WASM render modes register their own real one per-circuit;
        // this must never shadow that).
        services.TryAddScoped<IJSRuntime, UnsupportedJSRuntime>();
    }

    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        app.UseMiddleware<BlazorAdminThemeMiddleware>();
        app.UseCors(CrestWebCors);
        routes.MapHub<CrestPermissionHub>("/api/crest/permissions");
        routes.MapHub<CrestAdminMenuLayoutHub>("/api/crest/admin-menu-layout");

        // Interactive islands only (wasm/Pages/BlazorCounter.razor today) - the
        // endpoint this maps is what actually understands @rendermode, unlike
        // CrestBlazorComponentShapeBindingResolver's plain HtmlRenderer (see that
        // resolver's comments / plan doc Phase 3.5). AddAdditionalAssemblies pulls in
        // whichever theme wasm/*.Client.csproj projects OrchardCore.Crest.csproj's
        // CrestThemeWasmClientProject glob resolved to, so InteractiveWebAssembly/
        // InteractiveAuto components declared in any of them are discoverable too.
        // MapStaticAssets must precede AddInteractiveWebAssemblyRenderMode, or the
        // WASM runtime's static asset endpoints ("Mapped static asset endpoints not
        // found") never get registered.
        //
        // AppDomain.CurrentDomain.GetAssemblies() only returns assemblies already
        // loaded into the process - a ProjectReference alone doesn't guarantee that by
        // this point in Configure(), since .NET only loads a referenced assembly on
        // first actual use (JIT), and nothing has touched a *.Client type yet this
        // early in startup. Force each candidate to load via a throwaway
        // Assembly.LoadFrom on its own already-resolved location before re-querying
        // GetAssemblies(), or Routes/routable @page components living in the .Client
        // project (see docs/BlazorWeb.md) silently 404 - MapRazorComponents<App>()
        // built its route table before the assembly was ever loaded.
        foreach (var clientAssemblyPath in Directory.EnumerateFiles(
            AppContext.BaseDirectory, "*.Client.dll", SearchOption.TopDirectoryOnly))
        {
            System.Reflection.Assembly.LoadFrom(clientAssemblyPath);
        }

        routes.MapStaticAssets();
        routes.MapRazorComponents<App>()
            .AddInteractiveServerRenderMode()
            .AddInteractiveWebAssemblyRenderMode()
            .AddAdditionalAssemblies(AppDomain.CurrentDomain.GetAssemblies()
                .Where(assembly => assembly.GetName().Name?.EndsWith(".Client", StringComparison.Ordinal) == true)
                .ToArray());
    }
}

[Feature("OrchardCore.Crest.Icons.TenantMedia")]
public sealed class TenantMediaIconsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IIconProvider, TenantMediaIconProvider>();
        services.AddScoped<IPermissionProvider, Security.CrestIconPermissions>();
    }
}

[Feature("OrchardCore.Crest.DesignSystem")]
public sealed class DesignSystemStartup : StartupBase
{
}
