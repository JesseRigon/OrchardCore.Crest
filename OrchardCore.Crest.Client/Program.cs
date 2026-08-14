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

// The document base itself (tenantPrefix + shellBase + "/", from the <base href>
// BlazorAdminThemeMiddleware/App.razor composed) is the API base: every api/crest/*
// URL is issued relative to it and the server middleware strips the shell base back
// off ("/t2/Admin/api/..." -> tenant-root "/api/..."). The client therefore needs no
// origin-root or tenant-prefix knowledge at all - the previous authority-root base
// broke URL-prefixed tenants by escaping the tenant prefix entirely.
var appBaseAddress = new Uri(builder.HostEnvironment.BaseAddress);
var apiBaseAddress = appBaseAddress;

// Fetched anonymously, before Build(), the same way builder.HostEnvironment itself
// resolves appsettings.json - Login.razor and CrestAppContainer both need the
// tenant's real, configured AdminPath/LoginPath (see CrestRoutingController) to
// build cross-shell navigation targets the instant they render, before any
// authenticated API call could complete. Under InteractiveAuto this no longer blocks
// first paint (SSR and the server circuit cover it while WASM boots).
var routingOptions = await FetchRoutingOptionsAsync(apiBaseAddress);

// Cross-shell navigation targets must be browser-absolute, so compose the tenant
// base into them: the document base's path is tenantBase + shellBase + "/", and the
// fetched AdminPath/LoginPath tell us which shell suffix to peel off to recover
// tenantBase ("/t2/Login/" minus "/Login" -> "/t2"). Server-side DI mirrors this
// composition from Request.PathBase (see Startup.cs's CrestRoutingOptions factory).
var basePath = appBaseAddress.AbsolutePath.TrimEnd('/');
var tenantBase = basePath switch
{
    _ when basePath.EndsWith(routingOptions.AdminPath, StringComparison.OrdinalIgnoreCase)
        => basePath[..^routingOptions.AdminPath.Length],
    _ when basePath.EndsWith(routingOptions.LoginPath, StringComparison.OrdinalIgnoreCase)
        => basePath[..^routingOptions.LoginPath.Length],
    _ => basePath,
};
routingOptions.AdminPath = tenantBase + routingOptions.AdminPath;
routingOptions.LoginPath = tenantBase + routingOptions.LoginPath;

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
