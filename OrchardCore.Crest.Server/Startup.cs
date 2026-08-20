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

        // BlazorAdminThemeMiddleware rewrites Request.Path (tenant-configured admin
        // prefix -> compile-time @page literals, plus base-href-relative _framework/
        // _content/_blazor infrastructure URLs -> site root) - so it MUST run before
        // endpoint routing matches a path. OrchardCore's tenant pipeline calls
        // UseRouting() ahead of every module middleware regardless of ConfigureOrder
        // (ShellPipelineExtensions.ConfigurePipelineAsync), which is why this is an
        // IStartupFilter (applied before UseRouting, see BuildPipelineInternalAsync)
        // and not an app.UseMiddleware call in Configure() - registered there, its
        // rewrites happen after the endpoint is already selected and change nothing.
        services.AddTransient<Microsoft.AspNetCore.Hosting.IStartupFilter, BlazorAdminThemeStartupFilter>();
        services.AddSignalR();
        services.AddScoped<ICrestRequestAccess, CrestRequestAccess>();
        services.AddScoped<ICrestRoutePermissionProvider, CrestRoutePermissionProvider>();
        services.AddScoped<CrestRouteAuthorizationService>();
        services.AddScoped<ICrestPermissionInvalidator, CrestPermissionInvalidator>();
        services.AddScoped<ICrestAdminMenuLayoutInvalidator, CrestAdminMenuLayoutInvalidator>();
        services.AddScoped<IRoleUpdatedEventHandler, CrestRolePermissionInvalidationHandler>();
        services.AddScoped<IThemeSelector, LegacyFrameThemeSelector>();
        services.AddScoped<CrestAdminMenuLayoutService>();
        services.AddScoped<CrestAdminMenuTranslationService>();
        // Shell-lifetime, so the provider-menu import runs once per shell. A feature change
        // releases the shell, which is what makes the next shell re-import.
        services.AddSingleton<CrestProviderMenuSyncGate>();
        services.AddScoped<CrestProviderMenuSyncService>();
        services.AddScoped<CrestProviderMenuSyncCoordinator>();
        // Upstream's admin node localization providers enumerate root nodes only, which both
        // hides child captions from the Translations editor and lets its wholesale Save delete
        // their stored translations - see plans/upstream-orchard-proposals.md #2/#3 (fruitful).
        services.AddScoped<OrchardCore.Localization.Data.ILocalizationDataProvider, CrestAdminMenuChildCaptionDataLocalizationProvider>();
        // Deleting a content type deletes its display-name translations (unless another type
        // shares the name), so the store doesn't accumulate orphans no editor row can reach.
        services.AddScoped<OrchardCore.ContentTypes.Events.IContentDefinitionEventHandler, CrestContentTypeTranslationCleanup>();
        // Caption resolution for the sidebar and app manifest: restores the MenuName that
        // NavigationManager.Merge drops and walks parent/sibling translation contexts before
        // falling back to the invariant literal - see the resolver's remarks and
        // plans/upstream-orchard-proposals.md #7 (fruitful).
        services.AddScoped<CrestMenuCaptionResolver>();
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
        services.AddScoped<Crest.Routing.IBlazorAdminThemeDetector, Crest.Routing.BlazorAdminThemeDetector>();

        // RouteComponentTable: a per-shell, per-active-(admin+site)-theme-pair registry
        // mapping @page route patterns to their owning Blazor component Type, mirroring
        // DefaultShapeTableManager/ShapeTable's own caching shape (a keyed singleton
        // dictionary, no separate invalidation signal - the shell itself is torn down and
        // rebuilt by Orchard on feature/theme change). See
        // Crest.Routing.DefaultRouteComponentTableManager and docs/BlazorWeb.md's "Route
        // reachability" section for the full rationale. Each theme supplies its own
        // IRouteComponentTableProvider; nobody hand-maintains a central route list.
        services.AddSingleton(new System.Collections.Concurrent.ConcurrentDictionary<
            (string? AdminThemeId, string? SiteThemeId), Task<Crest.Routing.RouteComponentTable>>());
        services.AddScoped<Crest.Routing.IRouteComponentTableManager, Crest.Routing.DefaultRouteComponentTableManager>();
        services.AddScoped<Crest.Routing.IRouteComponentTableProvider, Crest.Routing.AdminRouteComponentTableProvider>();
        services.AddScoped<Crest.Routing.IRouteComponentTableProvider, Crest.Routing.SiteRouteComponentTableProvider>();

        // The gate itself - see Crest.Routing.RouteGateMatcherPolicy's own comments for
        // why this is a MatcherPolicy/IEndpointSelectorPolicy and not a
        // DynamicRouteValueTransformer or EndpointDataSource filter. Registered as
        // Microsoft.AspNetCore.Routing.Matching.MatcherPolicy, the base type ASP.NET
        // Core's endpoint selection pipeline discovers policies by (it enumerates every
        // registered MatcherPolicy, not just IEndpointSelectorPolicy directly).
        services.AddSingleton<Microsoft.AspNetCore.Routing.MatcherPolicy, Crest.Routing.RouteGateMatcherPolicy>();
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
        // "WASM as the primary route, Server as the fallback." Static-SSR-only content
        // (the overwhelming majority - anything rendered via
        // CrestBlazorComponentShapeBindingResolver's HtmlRenderer path) never touches
        // either of these; they only matter for components explicitly marked
        // @rendermode, i.e. genuine interactive islands like Components/Pages/
        // BlazorCounter.razor.
        services.AddRazorComponents()
            .AddInteractiveServerComponents()
            .AddInteractiveWebAssemblyComponents();

        // InteractiveAuto's first render runs server-side (SSR, then a SignalR
        // circuit) before a WASM runtime is even downloaded - every component that
        // calls a relative api/crest/* endpoint via HttpClient needs one with a real
        // BaseAddress during those server-side phases too, mirroring what the WASM
        // entry (OrchardCore.Crest.Client/Program.cs) sets up client-side. Scoped +
        // IHttpContextAccessor-derived base address: correct per-request even behind a
        // reverse proxy/different host header.
        //
        // Phase 8: wrapped in CrestForwardedAuthHandler - Admin components make
        // *authenticated* api/crest/* calls, and server-side there's no browser to
        // attach the Orchard auth cookie, so the handler forwards the incoming
        // request's own Cookie header (auth + antiforgery + culture cookies) and
        // fetches the antiforgery request token through it, exactly like the WASM
        // CrestAntiforgeryHandler does browser-side. Anonymous callers (Site's
        // BlazorCounter) are unaffected - forwarding an empty cookie set is a no-op.
        // Named handler registration (not AddHttpMessageHandler) so CrestForwardedAuthHandler
        // stays wired up per-request-scope explicitly below, instead of through
        // IHttpClientFactory's own handler pipeline - that pipeline resolves message
        // handlers from a rotating *internal* DI scope with its own lifetime (2 minutes
        // by default), not the calling request's scope, so a scoped handler registered
        // via AddHttpMessageHandler can get reused across unrelated requests within that
        // window (wrong user's forwarded cookies attached to someone else's request).
        // What AddHttpClient still buys us: a shared, pooled SocketsHttpHandler as the
        // *primary* handler, created once and reused - the actual fix for the socket
        // exhaustion (constructing `new HttpClientHandler()` per scope, as this used to,
        // opened a fresh unreused connection pool on every single request and eventually
        // exhausted ephemeral ports/file descriptors under load - observed as
        // SocketException "Resource temporarily unavailable" during a fresh-tenant
        // recipe run). CrestForwardedAuthHandler wraps that shared primary handler
        // fresh per scope below, so its own per-request state (captured cookies, cached
        // antiforgery token) stays correctly scoped while the sockets underneath it
        // don't.
        services.AddHttpClient("Crest.Server.Loopback.Primary");
        services.AddScoped(sp =>
        {
            var httpContext = sp.GetRequiredService<IHttpContextAccessor>().HttpContext!;
            var baseAddress = new Uri($"{httpContext.Request.Scheme}://{httpContext.Request.Host}{httpContext.Request.PathBase}/");
            var handler = sp.GetRequiredService<CrestForwardedAuthHandler>();
            handler.BaseAddress = baseAddress;
            handler.InnerHandler = sp.GetRequiredService<IHttpMessageHandlerFactory>().CreateHandler("Crest.Server.Loopback.Primary");
            return new HttpClient(handler) { BaseAddress = baseAddress };
        });
        services.AddScoped<CrestForwardedAuthHandler>();
        services.AddScoped<Crest.Admin.Api.ICrestAntiforgeryTokenStore>(sp => sp.GetRequiredService<CrestForwardedAuthHandler>());
        services.AddScoped<Crest.Admin.Api.ICrestCultureCookieWriter, CrestNoOpCultureCookieWriter>();
        services.AddCrestComponents();
        services.AddCrestIconClient();

        // Phase 8: the Admin theme's client-service set, server-side. Admin pages run
        // under InteractiveAuto, so their SSR/circuit phases resolve these from THIS
        // container - any admin-page dependency missing here is the AdminMenu DI bug
        // all over again (an unresolvable constructor surfacing as a blank/broken
        // page). Counterparts of Crest.Admin's AddCrestAdminClient (WASM side):
        // IApi/DisplayManager/CrestThemeEngine/CrestApiLocalizer ride the
        // forwarded-cookie HttpClient above; CrestRoutingOptions comes straight from
        // the tenant's configured options (no HTTP self-call needed server-side) via
        // BlazorAdminThemeOptions, which BlazorAdminThemeOptionsConfiguration already
        // post-configures from AdminOptions.AdminUrlPrefix/UserOptions.LoginPath.
        // Culture needs no explicit registration: CultureInfo.CurrentUICulture is
        // already resolved per-request by CrestCultureCookieOptionsConfiguration's
        // RequestLocalizationOptions pipeline, which CrestApiLocalizer picks up.
        services.AddScoped<Crest.Admin.Api.IApi, Crest.Admin.Api.Api>();
        services.AddScoped<Crest.Admin.DisplayManagement.DisplayManager>();
        services.AddScoped<Crest.Admin.Theme.CrestThemeEngine>();
        services.AddScoped<Crest.Components.Theme.CrestApiLocalizer>();
        services.AddScoped<Crest.Components.Primitives.ILocalizer>(sp => sp.GetRequiredService<Crest.Components.Theme.CrestApiLocalizer>());
        services.AddScoped(sp =>
        {
            var themeOptions = sp.GetRequiredService<IOptions<BlazorAdminThemeOptions>>().Value;
            // Composed on the tenant base so these stay real, navigable browser URLs
            // under URL-prefixed tenants, mirroring the WASM side's own composition
            // (OrchardCore.Crest.Client/Program.cs). During SSR of an admin page the
            // middleware has already shifted the shell base into PathBase, so the
            // pre-shift value it stashed is the tenant layer; on a circuit/hub request
            // PathBase was never shifted and is already exactly that layer.
            var httpContext = sp.GetRequiredService<IHttpContextAccessor>().HttpContext;
            var tenantBase = (httpContext?.Items.TryGetValue(CrestBlazorHosting.TenantBasePathItem, out var stashed) == true
                && stashed is string stashedBase
                    ? stashedBase
                    : httpContext?.Request.PathBase.Value ?? string.Empty).TrimEnd('/');
            return new Crest.Admin.Options.CrestRoutingOptions
            {
                AdminPath = tenantBase + themeOptions.AdminPath,
                LoginPath = tenantBase + themeOptions.LoginPath,
            };
        });

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
        // Two naming conventions feed the route table: theme client assemblies
        // (*.Client - Site.Client, Admin.Client, the OrchardCore.Crest.Client entry)
        // and module-contributed Blazor page libraries (*.BlazorWasm - e.g.
        // Accounting.BlazorWasm, the same set Admin.Client's generated
        // CrestModuleAssemblyRegistry loads browser-side; keep the two conventions in
        // sync or a module's pages route in one runtime and 404 in the other).
        foreach (var clientAssemblyPath in Directory.EnumerateFiles(AppContext.BaseDirectory, "*.Client.dll", SearchOption.TopDirectoryOnly)
            .Concat(Directory.EnumerateFiles(AppContext.BaseDirectory, "*.BlazorWasm.dll", SearchOption.TopDirectoryOnly)))
        {
            System.Reflection.Assembly.LoadFrom(clientAssemblyPath);
        }

        routes.MapStaticAssets();
        // The framework boot scripts are absent from the static web assets manifest by
        // construction (see BlazorFrameworkScriptEndpoints' own comment) - mapped as
        // ordinary tenant endpoints so tenant/admin prefix stripping applies to them
        // like everything else Orchard routes.
        routes.MapBlazorFrameworkScripts(
            serviceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<Startup>>());
        routes.MapRazorComponents<App>()
            .AddInteractiveServerRenderMode()
            .AddInteractiveWebAssemblyRenderMode()
            .AddAdditionalAssemblies(AppDomain.CurrentDomain.GetAssemblies()
                .Where(assembly => assembly.GetName().Name is { } name
                    && (name.EndsWith(".Client", StringComparison.Ordinal) || name.EndsWith(".BlazorWasm", StringComparison.Ordinal)))
                .ToArray())
            // Stamps Crest.Routing.ThemeOwnerMetadata onto each @page-attributed
            // component's own endpoint (not the root), keyed off the component's
            // declaring assembly - the same assembly-name check
            // AdminRouteComponentTableProvider/SiteRouteComponentTableProvider use, so
            // there's one source of truth for "which bucket owns this assembly's
            // routes", never a second copy. ComponentTypeMetadata (added by Blazor's own
            // RazorComponentEndpointFactory before conventions run) is what makes this
            // per-endpoint rather than a single global tag on the whole data source -
            // confirmed via decompiled Microsoft.AspNetCore.Components.Endpoints source.
            // Crest.Routing.RouteGateMatcherPolicy is the sole consumer. The metadata
            // carries a two-value RouteBucket, not a raw theme id - see
            // ThemeOwnerMetadata's own comment for why a theme-id comparison can never
            // disambiguate an Admin-vs-Site route collision (both a tenant's admin theme
            // and site theme are active at once, by construction).
            .Add(endpointBuilder =>
            {
                var componentType = endpointBuilder.Metadata
                    .OfType<Microsoft.AspNetCore.Components.Endpoints.ComponentTypeMetadata>()
                    .FirstOrDefault()?.Type;
                var assemblyName = componentType?.Assembly.GetName().Name;

                Crest.Routing.RouteBucket? bucket = assemblyName switch
                {
                    "OrchardCore.Crest.Admin.Client" => Crest.Routing.RouteBucket.Admin,
                    "OrchardCore.Crest.Site.Client" => Crest.Routing.RouteBucket.Site,
                    { } name when name.EndsWith(".BlazorWasm", StringComparison.Ordinal) => Crest.Routing.RouteBucket.Admin,
                    _ => null,
                };

                if (bucket is { } value)
                {
                    endpointBuilder.Metadata.Add(new Crest.Routing.ThemeOwnerMetadata(value));
                }
            });
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
