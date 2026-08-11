# Blazor Web App hosting in an OrchardCore module

Crest.Server hosts interactive Blazor islands (routable `.razor` pages marked `@rendermode`, e.g. `Components/Pages/BlazorCounter.razor`) directly from an OrchardCore module project. This isn't the stock `dotnet new blazor` template shape, and two parts of that template's usual "just works" behavior silently break when the host is an OrchardCore module instead of a normal ASP.NET Core web app. Both failures are silent at build time — no error, no warning, just a 404 or missing markup at runtime — so they're easy to reintroduce without noticing.

## Hosting model, in brief (stable — the design below is done, not in flux)

- **`OrchardCore.Crest.Server` is the single Blazor Web App host** for every Crest theme (Site, Admin, and any future Blazor-capable theme). No theme self-hosts `AddRazorComponents()`. Theme WASM client assemblies (`*.Client.csproj`) are discovered by an MSBuild glob, not a hardcoded `ProjectReference` — adding a new theme's client project never requires editing `Server.csproj`.
- **`Components/App.razor` is a theme-dispatching document root.** One `MapRazorComponents<App>()` call serves every theme; `App.razor` picks Admin's branch vs. Site's branch off `HttpContext.Items[CrestBlazorHosting.ShellBasePathItem]`, a marker stamped by `BlazorAdminThemeMiddleware` — never a raw path check, since that would bypass the middleware's theme-selection and route-authorization gates.
- **`InteractiveAuto` is the default render mode** for every page (server circuit first, WASM once cached), with one deliberate exception: the login page renders `InteractiveWebAssembly`. Under Auto's first-visit server circuit, a credential POST goes out on the *server-side* loopback `HttpClient`, so the auth `Set-Cookie` lands in that handler and never reaches the browser — login silently succeeds server-side while leaving the browser anonymous. Don't "simplify" this back to Auto.
- **Two separate `<Router>` components** (Admin's `AdminRoutes.razor`, Site's `Routes.razor`), each correctly scoped to its own `AppAssembly`/`AdditionalAssemblies`. This is right for client-side navigation *after* a component is already rendering, but does **not** scope which routes the server itself will match — see "What `<Router>` scoping does and doesn't do" below, and `plans/blazor hybrid conversion.md`'s routing-defect section for the still-open gap this creates.
- **Path configuration is never hardcoded.** `BlazorAdminThemeOptionsConfiguration` (`IPostConfigureOptions<BlazorAdminThemeOptions>`) sources `AdminPath`/`LoginPath` from Orchard's real tenant-configured `AdminOptions.AdminUrlPrefix`/`UserOptions.LoginPath` — a tenant on a custom prefix (e.g. `/backoffice`) works with zero code changes. `@page` directives in Admin's own pages are deliberately base-relative (`@page "/Features"`, not `@page "/Admin/Features"`) so they resolve correctly under whichever base href the middleware sets.
- **OrchardCore tenant pipeline ordering matters**: `ShellPipelineExtensions.ConfigurePipelineAsync` calls `UseRouting()` *before* any module's `Configure()` middleware runs, regardless of `ConfigureOrder`. A `Request.Path` rewrite in ordinary middleware is therefore invisible to endpoint matching. `BlazorAdminThemeMiddleware` only works because it's registered via `BlazorAdminThemeStartupFilter : IStartupFilter`, and Orchard runs startup filters *before* `UseRouting`.

## What `<Router>` scoping does and doesn't do

`MapRazorComponents<App>().AddAdditionalAssemblies(...)` builds **one server-side endpoint table** for the whole app, assembled once when the tenant shell's pipeline is built. `<Router>`'s `AppAssembly`/`AdditionalAssemblies` parameters do not scope that table — they only govern client-side/circuit navigation *after* a component is already rendering. Concretely: a bare `@page "/Features"` in Admin's assembly is matched by the endpoint router regardless of which `<Router>` would eventually receive control, because endpoint matching happens *before* `App.razor` (or either `<Router>`) runs. This is why Admin's unprefixed page routes are reachable at the public site root today, and why the fix has to live at the endpoint/gating layer, not by restructuring the routers. See the plan doc for the concrete fix design (an OrchardCore-native `EndpointDataSource`/feature-gating approach, not a Blazor-specific patch).

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

## Testing gotchas found converting Admin to SSR (Phase 8 triage, condensed)

These recur any time a page is converted from pure-WASM to Static SSR + `InteractiveAuto`. Full root-cause list lived in the plan doc during the conversion; kept here in short form since they're general lessons, not open work.

- **Prerender click race**: the SSR document contains real, visible buttons *before* the interactive runtime attaches handlers, so an early click is silently inert. Use a click-and-wait-for-effect helper with retry (`tests/playwright/harness/interactive.js`'s `clickForEffect`), not a plain click, on any control that must work immediately after page load. Don't use it on a *toggle* — the retry undoes a successful first click.
- **`fetch(path, { headers: {...}, ...options })`** lets `options.headers` (e.g. an antiforgery token alone) replace the merged headers object, silently dropping `content-type` → 415.
- **`RendererInfo.IsInteractive`, not `OperatingSystem.IsBrowser()`**, is the right prerender guard for logic that behaves differently under SSR (e.g. `NavigationManager.BaseUri` is the site root during prerender, not the real browser base — a `NavigateTo(absolute, forceLoad)` comparison against it can loop). Reserve `OperatingSystem.IsBrowser()` for genuinely browser-only things like starting a SignalR hub connection.
- **Blazor bool-attribute rendering**: a `true` bool parameter renders as `aria-expanded=""` (empty string); `false` **omits the attribute entirely**. Blazor never writes the literal string `"true"`/`"false"`. A DOM-reading test must check "attribute present and not `false`", not `=== 'true'`.
- **OrchardCore document-store read-your-own-writes**: `GetOrCreateImmutableAsync()` serves a cached document that does not reflect a write made via `GetOrCreateMutableAsync()` earlier in the *same* request — the deferred save commits after the response is sent. Any handler that mutates then reads back via the immutable accessor in the same request sees stale data; use the mutable/`LoadAsync()` path for the read-back instead.
- **Component dispose during prerender teardown**: `DisposeAsync` calling into JS interop can throw `InvalidOperationException`/`JSDisconnectedException` when the circuit already tore down. Guard fire-and-forget dispose calls against both.
- **A test can dirty a tracked file legitimately.** `menu-editor-layout-export.js` exercises a real export endpoint that writes into the repo's own `recipes/` directory by design — that's not test pollution, but genuine contamination (leftover nodes from a prior check's incomplete cleanup) can ride along in the same file. Diff it after any suite run rather than assuming either "it's always fine" or "it's always noise."

## Related ordering gotcha

`MapStaticAssets()` must be called before `.AddInteractiveWebAssemblyRenderMode()` on the same endpoint builder, or you'll get a `[WRN] Mapped static asset endpoints not found` at startup (harmless for Bug 2 above, since that 404 comes from a different missing asset, but worth fixing anyway):

```csharp
routes.MapStaticAssets();
routes.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode();
```
