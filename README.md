# BlazingOrchard OrchardCore Module

BlazingOrchard is a multi-project Orchard Core module repository for hosting Blazor component systems inside Orchard.

The repository is intentionally kept together for source management, but its projects are meant to remain independently packageable later.

```text
BlazingOrchard.OrchardCoreModule/
  BlazingOrchard.Server/
  BlazingOrchard.Components/
```

## Project Roles

`BlazingOrchard.Server` is the backend overlay on top of Orchard Core. It integrates with Orchard, serves the active Blazor admin shell when a Blazing-compatible admin theme is selected, exposes thin `api/blazing/*` JSON adapters for Blazor admin needs, and owns shared Orchard-side infrastructure such as legacy frame theme selection. It should call Orchard services, enforce Orchard permissions, and avoid owning duplicate CMS/auth/menu/theme state.

`BlazingOrchard.Components` contains the UI code for now. It is the current Radzen-based component and theme library, including reusable Blazor components, model/editor UI, client DTOs, the Blazing admin WASM shell, the Blazing site theme, CSS, JavaScript, and static assets. Theme projects may split into their own repositories/packages later, but currently remain under `BlazingOrchard.Components`.

Application modules that build UI for the current Radzen line reference `BlazingOrchard.Components` explicitly. For example, a new module `CRM.BlazorWasm` would reference the components project and contributes Blazor routes/components to the admin WASM build.

In the future, I'd like 3rd party modules to be able to call a 'generic' components from the shared components module as a standard library. This would enable custom component libraries to recreate them in their own style.

Another future feature I'd like to implement in the future is a standard API for modules/routes to declare and for Blazing Orchard to serve as WASM on their behalf.

## Runtime Model

Orchard remains the system of record for tenants, users, permissions, content, features, settings, themes, admin menus, and navigation.

The Blazing server module does not replace Orchard's APIs or rendering system. It is a backend adapter/overlay that uses Orchard services directly and exposes Blazor-friendly JSON only where Orchard's stock API surface is not enough for the client shell.

Preferred data-access order:

1. Use Orchard's built-in REST/JSON APIs when they satisfy the client contract.
2. Use Orchard GraphQL for content/query/read models where it fits.
3. Use Orchard Query API for configured reports and query-backed screens.
4. Use OpenID/JWT for external headless clients.
5. Add thin `api/blazing/*` adapters only for Blazor-specific projections/actions or Orchard functionality exposed only through MVC/Razor UI, services, or shapes.

## Blazor Admin Theme Serving

`BlazingOrchard.Server` installs middleware that checks the selected Orchard admin theme. If the selected admin theme is `BlazingOrchard.Admin` or has the configured Blazor tag, the middleware serves the Blazing admin WASM files for admin routes and Blazor assets.

The current admin shell assets live under:

```text
BlazingOrchard.Components/Themes/BlazingOrchard.Admin/wasm
```

The Orchard-loadable admin theme manifest project lives at:

```text
BlazingOrchard.Components/Themes/BlazingOrchard.Admin
```

## Component System Boundary

The current concrete UI implementation is Radzen-based. Radzen controls, Radzen CSS/JS, Blazor components, theme manifests, and Radzen-specific admin UI belong in `BlazingOrchard.Components` for now.

Shared runtime contracts and infrastructure should not depend on Radzen. Future component systems should be able to reuse the Orchard-side server runtime, JSON contracts, route/theme conventions, and legacy frame infrastructure without copying Radzen-specific code.

The planned neutral client/core package has not been extracted yet. Until it exists, some client contracts and display-management ideas still live inside `BlazingOrchard.Components` or the Blazing admin WASM project.

## Legacy Frame System

Legacy framing is shared Blazing infrastructure. It exists so normal Orchard admin pages can render inside the Blazing admin shell when no native Blazor route exists.

The Orchard-side legacy frame pieces live in `BlazingOrchard.Server`:

```text
BlazingOrchard.Server/LegacyFrameThemeSelector.cs
BlazingOrchard.Server/Themes/BlazingOrchard.LegacyFrame
```

Requests with `legacy-frame=1` or `legacy-frame=true` use the stripped `BlazingOrchard.LegacyFrame` admin theme. That theme keeps Orchard admin resources available while hiding the normal admin chrome so the page can sit inside an iframe.

The current iframe UI is still implemented inside the Radzen admin shell. A future neutral client package should own the reusable iframe component and URL-building behavior.

## Packaging Direction

The repository can remain a single git repository while publishing separate NuGet packages. The intended package boundaries are:

- `BlazingOrchard.Server`: Orchard runtime module and shared server infrastructure.
- `BlazingOrchard.Components`: Radzen-based component/theme implementation.
- Future `BlazingOrchard.Client`: UI-library-neutral client contracts, display manager, routing helpers, and legacy frame client component.
- Theme packages/repositories as needed if `BlazingOrchard.Admin`, `BlazingOrchard.Site`, or other Orchard-loadable themes are split out later.

Project files are not fully package-ready yet. Some projects still have `IsPackable=false`; packaging metadata and dependency boundaries need to be finalized before publishing.

## Validation

Use the host application for end-to-end validation:

```bash
dotnet build BlazingOrchard.Host.csproj --no-restore
```

Browser validation should use reusable Playwright scripts under `tests/playwright`, not one-off inline scripts.
