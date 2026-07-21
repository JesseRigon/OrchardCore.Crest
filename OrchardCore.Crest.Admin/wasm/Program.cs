using Crest.Admin;
using Crest.Admin.Api;
using Crest.Admin.DisplayManagement;
using Crest.Admin.Options;
using Crest.Admin.Theme;
using Crest.Icons;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Radzen;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.RootComponents.Add<App>("#app");

var appBaseAddress = new Uri(builder.HostEnvironment.BaseAddress);
var apiBaseAddress = new Uri(appBaseAddress.GetLeftPart(UriPartial.Authority) + "/");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = apiBaseAddress });
builder.Services.AddScoped<IApi, global::Crest.Admin.Api.Api>();
builder.Services.AddScoped<DisplayManager>();
builder.Services.AddScoped<CrestRoutingOptions>();
builder.Services.AddScoped<CrestThemeEngine>();
builder.Services.AddCrestIconClient();
builder.Services.AddRadzenComponents();

var app = builder.Build();

await app.RunAsync();
