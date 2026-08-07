using Crest.Components.Primitives;
using Crest.Icons;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace OrchardCore.Themes.Crest.Site;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection serviceCollection)
    {
        serviceCollection.AddResourceConfiguration<ResourceManagementOptionsConfiguration>();

        // Registered here so the DI machinery is in place ahead of Phase 3's routing
        // middleware. AddInteractiveWebAssemblyRenderMode's discovered assembly (the
        // Site.Client WASM project) is what makes @rendermode InteractiveWebAssembly/
        // InteractiveAuto components downloadable to the browser - nothing calls
        // MapRazorComponents yet, since Orchard's own dynamic, per-tenant routing owns
        // request dispatch until the middleware from Phase 3 exists to hand off to it
        // (see plans/blazor hybrid conversion.md's "Orchard's routing does not fit
        // stock Blazor Web App hosting" finding).
        serviceCollection.AddRazorComponents()
            .AddInteractiveWebAssemblyComponents();
        serviceCollection.AddCrestComponents();
        serviceCollection.AddCrestIconClient();
    }
}
