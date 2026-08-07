using Crest.Components.Primitives;
using Crest.Icons;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

// No RootComponents.Add<T>() here, unlike Admin's wasm/Program.cs - this project
// doesn't own an app shell to mount. It's the WASM half of Crest.Site's Blazor Web
// App split: interactive components declared @rendermode InteractiveAuto/
// InteractiveWebAssembly in the server project (OrchardCore.Crest.Site.csproj) are
// discovered and hosted from this assembly once the browser downloads it, via the
// server's own AddInteractiveWebAssemblyRenderMode().WithAdditionalAssemblies(...)
// wiring (Phase 3). Static-SSR-only components stay server-only and never need this
// project at all - that's what keeps the JS-disabled/crawler path working without it.

var apiBaseAddress = new Uri(builder.HostEnvironment.BaseAddress);
builder.Services.AddScoped(_ => new HttpClient { BaseAddress = apiBaseAddress });
builder.Services.AddCrestIconClient();
builder.Services.AddCrestComponents();

await builder.Build().RunAsync();
