using Elsa.Extensions;
using Elsa.Workflows;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Crest.Workflows.Queries.UI;
using OrchardCore.Modules;

namespace OrchardCore.Crest.Workflows.Queries;

public class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.ConfigureElsa(elsa =>
        {
            elsa.AddActivitiesFrom<Startup>();
        });

        services.AddScoped<IPropertyUIHandler, SqlCodeOptionsProvider>();
    }
}