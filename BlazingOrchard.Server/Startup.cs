using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using BlazingOrchard.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.DisplayManagement.Theming;
using OrchardCore.Modules;
using OrchardCore.Navigation;

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
        services.AddSingleton<BlazingIconSourceStore>();
        services.AddScoped<BlazingIconController>();
        services.Configure<BlazorAdminThemeOptions>(options => { });
    }

    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        app.UseMiddleware<BlazorAdminThemeMiddleware>();
        app.UseCors(BlazingWebCors);
    }
}

[Feature("BlazingOrchard.DesignSystem")]
public sealed class DesignSystemStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddNavigationProvider<BlazingAdminMenu>();
    }
}
