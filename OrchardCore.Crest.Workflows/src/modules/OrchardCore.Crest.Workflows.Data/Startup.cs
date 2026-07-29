using Elsa.Extensions;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace OrchardCore.Crest.Workflows.Data;

[Feature("OrchardCore.Crest.Workflows.Data.Csv")]
public class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.ConfigureElsa(elsa =>
        {
            elsa.UseCsv();
        });
    }
}