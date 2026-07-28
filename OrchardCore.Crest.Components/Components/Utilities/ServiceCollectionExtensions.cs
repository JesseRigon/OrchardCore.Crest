using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Crest.Components.Primitives;

/// <summary>
/// Class with IServiceCollection extensions methods.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Add Crest Blazor components services
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddCrestComponents(this IServiceCollection services)
    {
        services.AddScoped<DialogService>();
        services.AddScoped<NotificationService>();
        services.AddScoped<TooltipService>();
        services.AddScoped<ContextMenuService>();
        services.AddScoped<ThemeService>();
        services.TryAddScoped(sp => new Localizer(sp.GetService<ILocalizer>()));
        services.AddAIChatService();

        return services;
    }
}
