using BlazingOrchard.Admin;
using BlazingOrchard.Admin.Api;
using BlazingOrchard.Admin.DisplayManagement;
using BlazingOrchard.Admin.Options;
using BlazingOrchard.Admin.Theme;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Radzen;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.RootComponents.Add<App>("#app");

var appBaseAddress = new Uri(builder.HostEnvironment.BaseAddress);
var apiBaseAddress = new Uri(appBaseAddress.GetLeftPart(UriPartial.Authority) + "/");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = apiBaseAddress });
builder.Services.AddScoped<IApi, global::BlazingOrchard.Admin.Api.Api>();
builder.Services.AddScoped<DisplayManager>();
builder.Services.AddScoped<BlazingRoutingOptions>();
builder.Services.AddScoped<BlazingThemeEngine>();
builder.Services.AddRadzenComponents();

var app = builder.Build();

await app.RunAsync();
