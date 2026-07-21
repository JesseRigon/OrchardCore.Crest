using Microsoft.Extensions.DependencyInjection;

namespace BlazingOrchard.Icons;

public static class BlazingIconClientServiceCollectionExtensions
{
    public static IServiceCollection AddBlazingOrchardIconClient(this IServiceCollection services)
    {
        services.AddScoped<IBlazingIconSearchClient, BlazingIconSearchClient>();
        services.AddSingleton<ClientIconRegistry>();

        return services;
    }
}
