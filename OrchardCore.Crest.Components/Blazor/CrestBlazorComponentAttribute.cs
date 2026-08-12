namespace Crest.Components.Blazor;

// Marks a Razor component as available for tenants to place via CrestBlazorComponentPart.
// Lives in Components (not Server) deliberately: components are authored here (and in any
// theme's own shared component library), while the registries that scan for this marker
// live wherever they're consumed - Server's SSR-side ICrestBlazorComponentRegistry today,
// and a WASM-side mirror in Admin's client project later (for native, interactive
// rendering in a future tenant-facing component builder - not yet implemented).
// Components must never reference Server (Server -> Components is the only allowed
// direction - Server is the single Blazor Web App host for every Crest theme, never the
// reverse), so the marker has to live on the Components side of that edge for either
// registry to compile against it.
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class CrestBlazorComponentAttribute(string name) : Attribute
{
    public string Name { get; } = name;
}
