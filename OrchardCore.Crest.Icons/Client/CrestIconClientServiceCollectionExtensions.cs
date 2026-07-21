using Microsoft.Extensions.DependencyInjection;

namespace Crest.Icons;

public static class CrestIconClientServiceCollectionExtensions
{
    public static IServiceCollection AddCrestIconClient(this IServiceCollection services)
    {
        services.AddScoped<ICrestIconSearchClient, CrestIconSearchClient>();
        services.AddSingleton<ClientIconRegistry>();

        return services;
    }
}
