using Elsa.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Environment.Shell.Configuration;
using OrchardCore.Modules;

namespace OrchardCore.Crest.Workflows.Features;

[Feature("OrchardCore.Crest.Workflows.Http")]
public class HttpStartup(IShellConfiguration shellConfiguration) : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.ConfigureElsa(elsa =>
        {
            elsa.UseHttp(http =>
            {
                http.ConfigureHttpOptions = options =>
                {
                    shellConfiguration.GetSection("CrestWorkflows:Http").Bind(options);
                };
                http.UseCache();
            });
        });

    }

    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        app.UseWorkflows();
    }
}