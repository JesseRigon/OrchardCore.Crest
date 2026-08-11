using System.Net.Http.Json;
using Crest.Admin;
using Crest.Admin.Options;
using Crest.Components.Primitives;
using Crest.Icons;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

// Phase 8: the one WASM boot path for the whole Crest Blazor Web App. blazor.web.js
// activates root components (the theme Routes mounted by Crest.Server's
// Components/App.razor with @rendermode InteractiveAuto) - no RootComponents.Add<T>()
// here. Service registrations merge what Site's and Admin's separate wasm/Program.cs
// files used to do; Admin's antiforgery-wrapped HttpClient becomes THE HttpClient for
// everything client-side (a strict superset of Site's old plain one - the antiforgery
// header is only attached to unsafe requests, credentials are same-origin anyway).
var builder = WebAssemblyHostBuilder.CreateDefault(args);

var appBaseAddress = new Uri(builder.HostEnvironment.BaseAddress);
var apiBaseAddress = new Uri(appBaseAddress.GetLeftPart(UriPartial.Authority) + "/");

// Fetched anonymously, before Build(), the same way builder.HostEnvironment itself
// resolves appsettings.json - Login.razor and CrestAppContainer both need the
// tenant's real, configured AdminPath/LoginPath (see CrestRoutingController) to
// build cross-shell navigation targets the instant they render, before any
// authenticated API call could complete. Under InteractiveAuto this no longer blocks
// first paint (SSR and the server circuit cover it while WASM boots).
var routingOptions = await FetchRoutingOptionsAsync(apiBaseAddress);

builder.Services.AddCrestAdminClient(apiBaseAddress, routingOptions);
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
