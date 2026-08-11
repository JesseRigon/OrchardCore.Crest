using Crest.Admin.Api;
using Crest.Admin.DisplayManagement;
using Crest.Admin.Options;
using Crest.Admin.Theme;
using Crest.Components.Primitives;
using Crest.Components.Theme;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace Crest.Admin;

// Phase 8: these registrations lived in wasm/Program.cs when this project was a
// standalone WASM app. As a client library under the single Blazor Web App entry
// (OrchardCore.Crest.Client/Program.cs), the entry calls this once during WASM boot.
// Everything here is the browser-side implementation set - Crest.Server registers its
// own server-side counterparts (per-request HttpClient with forwarded auth cookie,
// CrestRoutingOptions from AdminOptions/UserOptions, etc.) for the SSR/InteractiveServer
// phases of InteractiveAuto.
public static class CrestAdminClientServiceCollectionExtensions
{
    public static IServiceCollection AddCrestAdminClient(this IServiceCollection services, Uri apiBaseAddress, CrestRoutingOptions routingOptions)
    {
        services.AddScoped(sp => new CrestAntiforgeryHandler((IJSInProcessRuntime)sp.GetRequiredService<IJSRuntime>()) { BaseAddress = apiBaseAddress });
        services.AddScoped<ICrestAntiforgeryTokenStore>(sp => sp.GetRequiredService<CrestAntiforgeryHandler>());
        services.AddScoped<ICrestCultureCookieWriter>(sp => sp.GetRequiredService<CrestAntiforgeryHandler>());
        services.AddScoped(sp =>
        {
            var handler = sp.GetRequiredService<CrestAntiforgeryHandler>();
            handler.InnerHandler = new HttpClientHandler();
            return new HttpClient(handler) { BaseAddress = apiBaseAddress };
        });
        services.AddScoped<IApi, global::Crest.Admin.Api.Api>();
        services.AddScoped<DisplayManager>();
        services.AddScoped(_ => routingOptions);
        services.AddScoped<CrestThemeEngine>();
        services.AddScoped<CrestApiLocalizer>();
        services.AddScoped<ILocalizer>(sp => sp.GetRequiredService<CrestApiLocalizer>());
        return services;
    }
}
