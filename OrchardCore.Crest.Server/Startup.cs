using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Crest.Iconify;
using Crest.Icons;
using Crest.Services;
using Microsoft.Extensions.DependencyInjection;
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
        services.AddCrestCultureCookieProvider();
    }

    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        app.UseMiddleware<BlazorAdminThemeMiddleware>();
        app.UseCors(CrestWebCors);
        routes.MapHub<CrestPermissionHub>("/api/crest/permissions");
        routes.MapHub<CrestAdminMenuLayoutHub>("/api/crest/admin-menu-layout");
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
