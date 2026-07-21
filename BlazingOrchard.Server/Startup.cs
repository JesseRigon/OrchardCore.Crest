using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using BlazingOrchard.Icons;
using BlazingOrchard.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.DisplayManagement.Theming;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Security.Permissions;

namespace BlazingOrchard;

[Feature("Blazing")]
public sealed class Startup : StartupBase
{
    private const string BlazingWebCors = "BlazingWeb";

    public override int Order => -1000;

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCors(options =>
        {
            options.AddPolicy(BlazingWebCors, policy => policy
                .WithOrigins("http://localhost:5011", "http://127.0.0.1:5011")
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials());
        });

        services.AddHttpContextAccessor();
        services.AddScoped<IThemeSelector, LegacyFrameThemeSelector>();
        services.AddScoped<BlazingAdminMenuLayoutService>();
        services.AddNavigationProvider<BlazingAdminMenu>();
        services.AddScoped<IIconProvider, IconifyIconProvider>();
        services.AddScoped<IIconProviderSettingsStore, BlazingIconProviderSettingsStore>();
        services.AddSingleton<IIconifyLocalMirrorPathProvider, BlazingIconifyLocalMirrorPathProvider>();
        services.AddSingleton<IIconifyLocalMirrorStore, IconifyLocalMirrorStore>();
        services.AddHostedService<IconifyCacheRefreshService>();
        services.AddSingleton<SvgIconSanitizer>();
        services.AddHttpClient("BlazingOrchard.Icons.Iconify");
        services.AddScoped<IIconRegistry, CompositeIconRegistry>();
        services.AddScoped<BlazingIconSourceStore>();
        services.AddScoped<BlazingIconController>();
        services.Configure<BlazorAdminThemeOptions>(options => { });
    }

    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        app.UseMiddleware<BlazorAdminThemeMiddleware>();
        app.UseCors(BlazingWebCors);
    }
}

[Feature("BlazingOrchard.Icons.TenantMedia")]
public sealed class TenantMediaIconsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IIconProvider, TenantMediaIconProvider>();
        services.AddScoped<IPermissionProvider, Security.BlazingIconPermissions>();
    }
}

[Feature("BlazingOrchard.DesignSystem")]
public sealed class DesignSystemStartup : StartupBase
{
}
