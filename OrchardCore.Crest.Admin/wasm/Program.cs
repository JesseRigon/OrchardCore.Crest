using System.Net.Http.Json;
using Crest.Admin;
using Crest.Admin.Api;
using Crest.Admin.DisplayManagement;
using Crest.Admin.Options;
using Crest.Admin.Theme;
using Crest.Icons;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Crest.Components.Primitives;
using Crest.Components.Theme;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.RootComponents.Add<App>("#app");

var appBaseAddress = new Uri(builder.HostEnvironment.BaseAddress);
var apiBaseAddress = new Uri(appBaseAddress.GetLeftPart(UriPartial.Authority) + "/");

// Fetched anonymously, before Build(), the same way builder.HostEnvironment itself
// resolves appsettings.json - Login.razor and CrestAppContainer both need the
// tenant's real, configured AdminPath/LoginPath (see CrestRoutingController) to
// build cross-shell navigation targets the instant they render, before any
// authenticated API call could complete.
var routingOptions = await FetchRoutingOptionsAsync(apiBaseAddress);

builder.Services.AddScoped(sp => new CrestAntiforgeryHandler((Microsoft.JSInterop.IJSInProcessRuntime)sp.GetRequiredService<Microsoft.JSInterop.IJSRuntime>()) { BaseAddress = apiBaseAddress });
builder.Services.AddScoped<ICrestAntiforgeryTokenStore>(sp => sp.GetRequiredService<CrestAntiforgeryHandler>());
builder.Services.AddScoped(sp =>
{
    var handler = sp.GetRequiredService<CrestAntiforgeryHandler>();
    handler.InnerHandler = new HttpClientHandler();
    return new HttpClient(handler) { BaseAddress = apiBaseAddress };
});
builder.Services.AddScoped<IApi, global::Crest.Admin.Api.Api>();
builder.Services.AddScoped<DisplayManager>();
builder.Services.AddScoped(_ => routingOptions);
builder.Services.AddScoped<CrestThemeEngine>();
builder.Services.AddScoped<CrestApiLocalizer>();
builder.Services.AddScoped<ILocalizer>(sp => sp.GetRequiredService<CrestApiLocalizer>());
builder.Services.AddCrestIconClient();
builder.Services.AddCrestComponents();

var app = builder.Build();

await app.RunAsync();

static async Task<CrestRoutingOptions> FetchRoutingOptionsAsync(Uri apiBaseAddress)
{
    try
    {
        using var client = new HttpClient { BaseAddress = apiBaseAddress };
        var response = await client.GetFromJsonAsync<CrestRoutingResponse>("api/crest/routing");
        if (response is not null)
        {
            return new CrestRoutingOptions { AdminPath = response.AdminPath, LoginPath = response.LoginPath };
        }
    }
    catch (HttpRequestException)
    {
    }

    return new CrestRoutingOptions();
}

internal sealed record CrestRoutingResponse(string AdminPath, string LoginPath);
