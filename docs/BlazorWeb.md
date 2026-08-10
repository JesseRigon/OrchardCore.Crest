# Blazor Web App hosting in an OrchardCore module

Crest.Server hosts interactive Blazor islands (routable `.razor` pages marked `@rendermode`, e.g. `Components/Pages/BlazorCounter.razor`) directly from an OrchardCore module project. This isn't the stock `dotnet new blazor` template shape, and two parts of that template's usual "just works" behavior silently break when the host is an OrchardCore module instead of a normal ASP.NET Core web app. Both failures are silent at build time — no error, no warning, just a 404 or missing markup at runtime — so they're easy to reintroduce without noticing.

## Bug 1: `.razor` files silently excluded from compilation

`OrchardCore.Module.Targets` (the shared MSBuild package every Crest module references) sets `EnableDefaultRazorGenerateItems=false` project-wide and only re-adds `RazorGenerate` for legacy `.cshtml` files. It predates Blazor component support and has no concept of `.razor` at all. Without an explicit override, every `.razor` file under a module's own folder (not just consumed via a `ProjectReference` to a components library) is invisible to the compiler — not a `RazorComponent`, not even a `Compile` item.

**Symptom:** the page 404s, or a type like `App`/`Routes` fails to resolve, even though the `.razor` file is clearly present on disk.

**Fix** — add explicitly in the module's `.csproj` (see `OrchardCore.Crest.Server/OrchardCore.Crest.csproj`):

```xml
<ItemGroup>
  <RazorComponent Include="Components\**\*.razor" Exclude="Themes\**" />
</ItemGroup>
```

**Diagnose:** `dotnet build -v:diag` and look for the file only appearing as `ModuleAssets`/`EmbeddedResource` (via the `OrchardCoreEmbedModuleAssets` target), never as `Compile`/`RazorComponent`/`RazorGenerate`. `-getItem` is unreliable for this — it can return empty `RazorComponent` lists even for projects that build fine.

## Bug 2: `_framework/blazor.web.js` 404s — the interactive island is inert

`blazor.web.js` (and `blazor.server.js`) ship via the `microsoft.aspnetcore.app.internal.assets` SDK package's own static-web-assets target (`_AddBlazorFrameworkStaticWebAssets`). That target only fires when **both** `OutputType == Exe` and `UsingMicrosoftNETSdkWeb == true`. A Crest module project is `Sdk="Microsoft.NET.Sdk.Razor"` with `OutputType=Library` (the standard OrchardCore module shape) — it fails both conditions, so the file never reaches the static web assets manifest, regardless of whether `Microsoft.AspNetCore.Components.WebAssembly.Server` is referenced.

**Symptom:** the page renders (markup, seeded data, everything looks right), but no `@onclick`/interactivity ever fires. No console error unless you check the network tab: `_framework/blazor.web.js` returns 404. No exception anywhere in the server log — this is easy to mistake for a render-mode/`@rendermode` wiring mistake, but it isn't; `MapRazorComponents<App>().AddInteractiveServerRenderMode().AddInteractiveWebAssemblyRenderMode()` can be wired perfectly and this will still fail.

**Confirmed not the cause, ruled out during investigation:**
- `@rendermode` wiring itself (server/wasm/auto) — correct wiring still 404s without this fix.
- Orchard's shell-scoped `IEndpointRouteBuilder` — it's the same live `IEndpointRouteBuilder`/`DataSources` as the real request pipeline (Orchard's `ShellPipelineExtensions.BuildPipelineInternalAsync` is the only place `UseRouting()`/`UseEndpoints()` run), so `MapStaticAssets()` inside a module `Startup.Configure()` is correctly wired — it just has nothing to serve, because the asset was never in the manifest to begin with.
- Setting `OutputType=Exe` on the module project directly *would* satisfy the SDK gate, but risks breaking `OrchardCore.Module.Targets`' embedded-resource-based module asset packaging, which assumes a library. Not used here.

**Fix** — serve the file directly from the same physical location the SDK target would have used, scoped to `/_framework` only, registered once in the host's top-level `Program.cs` (see `OrchardCore.Crest.Host/Program.cs`):

```csharp
var frameworkAssetsRoot = Directory
    .EnumerateDirectories(Path.Combine(
        Environment.GetEnvironmentVariable("NUGET_PACKAGES")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages"),
        "microsoft.aspnetcore.app.internal.assets"))
    .OrderByDescending(path => path)
    .Select(path => Path.Combine(path, "_framework"))
    .FirstOrDefault(Directory.Exists);

if (frameworkAssetsRoot is not null)
{
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(frameworkAssetsRoot),
        RequestPath = "/_framework",
    });
}
```

This resolves the package version dynamically (no hardcoded version string to go stale on an SDK upgrade) and only needs to exist once per host, not per module.

**Diagnose:** `curl -o /dev/null -w '%{http_code}' http://<host>/_framework/blazor.web.js` — if it's not 200, no interactive island on that host will ever be clickable, no matter how correct the `.razor`/`Startup.cs` wiring looks. Check this *before* debugging render-mode code.

## Related ordering gotcha

`MapStaticAssets()` must be called before `.AddInteractiveWebAssemblyRenderMode()` on the same endpoint builder, or you'll get a `[WRN] Mapped static asset endpoints not found` at startup (harmless for Bug 2 above, since that 404 comes from a different missing asset, but worth fixing anyway):

```csharp
routes.MapStaticAssets();
routes.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode();
```
