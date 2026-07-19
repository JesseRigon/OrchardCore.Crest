# BlazingOrchard.Components

`BlazingOrchard.Components` contains the UI code for Blazing Orchard for now. It is the current Radzen-based Blazor component and theme library, with reusable client-side components, shared client models, experimental shape/model helpers, and the Orchard-loadable themes that make up the current WebAssembly admin experience.

The current implementation is **Blazor WebAssembly-first**. Components run in the browser and call Orchard or thin `api/blazing/*` adapters exposed by `BlazingOrchard.Server`; Orchard remains the backend authority for tenants, users, permissions, content, features, settings, themes, and navigation.

Boundary rule: UI belongs here; backend integration belongs in `BlazingOrchard.Server`. This project should not duplicate Orchard's auth, permissions, content-management, menu-building, tenant, or theme-application logic. It should render UI and call Orchard-backed APIs.

## Project layout

```text
BlazingOrchard.Components/
  Components/                    # reusable Blazor components
    Forms/
    Inputs/
    Model/
  Models/                        # client-safe DTOs and request models
  Shapes/                        # early client-side shape/model experiments
  Themes/
    BlazingOrchard.Admin/        # Orchard admin theme manifest
      wasm/                      # Blazor WebAssembly admin shell
    BlazingOrchard.Site/         # included Orchard site theme
```

## BlazingOrchard.Admin

`Themes/BlazingOrchard.Admin` is the Orchard admin theme entry point and current Blazing admin UI host. It has two parts:

- `BlazingOrchard.Admin.csproj` and `Manifest.cs` register the admin theme with Orchard.
- `wasm/BlazingOrchard.Admin.Wasm.csproj` builds the browser-side Blazor WebAssembly admin shell.

The WASM shell owns the current admin routes, sidebar layout, Radzen UI, legacy-frame fallback UI, and module-contributed Blazor route loading. It references `BlazingOrchard.Components` for shared components such as model lists, editors, and inputs. Server-side route interception, Orchard service access, permissions, and `api/blazing/*` adapters remain in `BlazingOrchard.Server`.

## BlazingOrchard.Site

`Themes/BlazingOrchard.Site` is the included standard Orchard site theme for Blazing Orchard test/dev setups. It is a normal Orchard theme project with its own `Manifest.cs`, `Startup.cs`, views, static assets, and recipe assets.

The site theme is separate from the Blazing admin shell. It exists so a host can enable both a Blazing admin theme and a known site theme from the same module repository. It may become a separate project repository/package later, but it currently stays here with the rest of the UI/theme code.

## Module component convention

Third-party Orchard modules can currently contribute Blazing admin UI at build time by adding a WASM project shaped like:

```text
modules/{ModuleName}/blazor-wasm/*.csproj
```

The admin WASM project discovers these projects, references them, generates a module assembly registry, and passes those assemblies to the Blazor router as additional assemblies. This is a build-time convention today, not a finalized runtime plugin API.

Future work should formalize this into a module manifest/registry model so enabled Orchard modules can declare routes, assemblies, scripts, styles, and permissions more explicitly.

## Versioning

Blazing Orchard uses a five-part compatibility version:

```text
{orchard-major}.{orchard-minor}.{orchard-patch}.{blazing-security}.{blazing-bug}
```

The first three parts identify the Orchard Core version tested with this build. The last two parts are Blazing Orchard's security and bug-fix counters.

Current compatibility version: `3.0.0.0.0`.
