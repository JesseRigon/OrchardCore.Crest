# OrchardCore.Crest OrchardCore Module

OrchardCore.Crest is a multi-project Orchard Core module repository for hosting Blazor component systems inside Orchard.

The repository is intentionally kept together for source management, but its projects are meant to remain independently packageable later.

```text
OrchardCore.Crest/
  OrchardCore.Crest.Server/
  OrchardCore.Crest.Components/
  OrchardCore.Crest.Icons/
  OrchardCore.Crest.Admin/
  OrchardCore.Crest.Site/
```

## Project Roles

`OrchardCore.Crest.Server` is the backend overlay on top of Orchard Core. It integrates with Orchard, serves the active Blazor admin shell when a Crest-compatible admin theme is selected, exposes thin `api/crest/*` JSON adapters for Blazor admin needs, and owns shared Orchard-side infrastructure such as legacy frame theme selection. It should call Orchard services, enforce Orchard permissions, and avoid owning duplicate CMS/auth/menu/theme state.

`OrchardCore.Crest.Components` is the shared Radzen-backed Blazor component layer. It owns reusable primitives, forms, model/editor UI, and client-safe UI contracts. It must not reference feature modules.

`OrchardCore.Crest.Icons` owns icon providers, registry/search/pack services, icon UI such as `IconSelector`, and icon-specific CSS/JS/assets. It may depend on `OrchardCore.Crest.Components`.

Admin and Site themes are composition roots. They reference `OrchardCore.Crest.Components`, `OrchardCore.Crest.Icons`, and other feature UI modules they want compiled into the WASM app.

Application modules that build UI for the current Radzen line reference `OrchardCore.Crest.Components` explicitly. For example, a new module `CRM.BlazorWasm` would reference the components project and contributes Blazor routes/components to the admin WASM build.

In the future, I'd like 3rd party modules to be able to call a 'generic' components from the shared components module as a standard library. This would enable custom component libraries to recreate them in their own style.

Another future feature I'd like to implement in the future is a standard API for modules/routes to declare and for Orchard Crest UI Framework to serve as WASM on their behalf.

## Runtime Model

Orchard remains the system of record for tenants, users, permissions, content, features, settings, themes, admin menus, and navigation.

The Crest server module does not replace Orchard's APIs or rendering system. It is a backend adapter/overlay that uses Orchard services directly and exposes Blazor-friendly JSON only where Orchard's stock API surface is not enough for the client shell.

Preferred data-access order:

1. Use Orchard's built-in REST/JSON APIs when they satisfy the client contract.
2. Use Orchard GraphQL for content/query/read models where it fits.
3. Use Orchard Query API for configured reports and query-backed screens.
4. Use OpenID/JWT for external headless clients.
5. Add thin `api/crest/*` adapters only for Blazor-specific projections/actions or Orchard functionality exposed only through MVC/Razor UI, services, or shapes.

## Blazor Admin Theme Serving

`OrchardCore.Crest.Server` installs middleware that checks the selected Orchard admin theme. If the selected admin theme is `OrchardCore.Crest.Admin` or has the configured Blazor tag, the middleware serves the Crest admin WASM files for admin routes and Blazor assets.

The current admin shell assets still live under:

```text
OrchardCore.Crest.Admin/wasm
```

The Orchard-loadable admin theme manifest project still lives at:

```text
OrchardCore.Crest.Admin
```

## Component System Boundary

The current concrete UI implementation is Radzen-based. Shared Radzen-backed primitives belong in `OrchardCore.Crest.Components`. Feature UI and assets belong with their feature modules; theme chrome belongs with theme projects.

Shared runtime contracts and infrastructure should not depend on Radzen. Future component systems should be able to reuse the Orchard-side server runtime, JSON contracts, route/theme conventions, and legacy frame infrastructure without copying Radzen-specific code.

The planned neutral client/core package has not been extracted yet. Until it exists, some client contracts and display-management ideas still live inside `OrchardCore.Crest.Components` or the Crest admin WASM project.

## Legacy Frame System

Legacy framing is shared Crest infrastructure. It exists so normal Orchard admin pages can render inside the Crest admin shell when no native Blazor route exists.

The Orchard-side legacy frame pieces live in `OrchardCore.Crest.Server`:

```text
OrchardCore.Crest.Server/LegacyFrameThemeSelector.cs
OrchardCore.Crest.Server/Themes/OrchardCore.Crest.LegacyFrame
```

Requests with `legacy-frame=1` or `legacy-frame=true` use the stripped `OrchardCore.Crest.LegacyFrame` admin theme. That theme keeps Orchard admin resources available while hiding the normal admin chrome so the page can sit inside an iframe.

The current iframe UI is still implemented inside the Radzen admin shell. A future neutral client package should own the reusable iframe component and URL-building behavior.

## Packaging Direction

The repository can remain a single git repository while publishing separate NuGet packages. The intended package boundaries are:

- `OrchardCore.Crest.Server`: Orchard runtime module and shared server infrastructure.
- `OrchardCore.Crest.Components`: shared Radzen-backed component layer.
- `OrchardCore.Crest.Icons`: icon providers, icon UI, and icon-owned assets.
- Theme packages/projects: admin/site composition roots that reference components and feature modules.
- Future `OrchardCore.Crest.Client`: UI-library-neutral client contracts, display manager, routing helpers, and legacy frame client component.

Project files are not fully package-ready yet. Some projects still have `IsPackable=false`; packaging metadata and dependency boundaries need to be finalized before publishing.

## Development Recipes

Reusable Crest development recipes live under `OrchardCore.Crest.Site/Recipes`: `OrchardCore.CrestBasicDev` for a focused Crest admin shell and `OrchardCore.CrestFullDev` for broad Orchard/Crest feature testing. Host apps should keep tenant/user autosetup recipes in the host repo.

## Validation

Use the host application for end-to-end validation:

```bash
dotnet build OrchardCore.Crest.Host.csproj --no-restore
```

Browser validation should use reusable Playwright scripts under the owning project's `tests/playwright` directory, not one-off inline scripts.
