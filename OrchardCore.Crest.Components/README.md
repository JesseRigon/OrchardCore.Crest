# OrchardCore.Crest.Components

`OrchardCore.Crest.Components` is the shared Radzen-backed Blazor component layer for Orchard Crest UI Framework. It owns reusable client-side primitives, forms, model/editor UI, shared client models, and experimental shape/model helpers.

The current implementation is **Blazor WebAssembly-first**. Components run in the browser and call Orchard or thin `api/crest/*` adapters exposed by `OrchardCore.Crest.Server`; Orchard remains the backend authority for tenants, users, permissions, content, features, settings, themes, and navigation.

Boundary rule: shared Radzen-backed UI primitives belong here; backend integration belongs in `OrchardCore.Crest.Server`; feature-specific UI belongs with the feature module that owns it. This project must not reference feature modules such as `OrchardCore.Crest.Icons`, CRM, or Commerce.

## Project layout

```text
OrchardCore.Crest.Components/
  Components/                    # reusable Blazor components
    Forms/
    Inputs/
    Navigation/
    Model/
  Models/                        # client-safe DTOs and request models
  Shapes/                        # early client-side shape/model experiments
```

## Theme boundary

Admin and Site themes are composition roots. They should reference `OrchardCore.Crest.Components`, `OrchardCore.Crest.Icons`, and other module UI assemblies they need.

Reusable layout and navigation mechanics belong in this project. Theme-specific components should pass options into these primitives rather than hard-coding rail widths, indentation, animation timings, or compact behavior in feature CSS.

## Feature UI boundary

Feature modules own their larger UI components and assets.

For example, `OrchardCore.Crest.Icons` owns `IconSelector`, icon registry/search UI, and icon-specific CSS/JS. Components may provide shared primitives used by those modules, but it should not reach into them.

## Module component convention

Third-party Orchard modules can currently contribute Crest admin UI at build time by adding a WASM project shaped like:

```text
modules/{ModuleName}/blazor-wasm/*.csproj
```

The admin WASM project discovers these projects, references them, generates a module assembly registry, and passes those assemblies to the Blazor router as additional assemblies. This is a build-time convention today, not a finalized runtime plugin API.

Future work should formalize this into a module manifest/registry model so enabled Orchard modules can declare routes, assemblies, scripts, styles, editor components, and permissions more explicitly. A later runtime lane may serve compiled WASM module bundles from `App_Data/wasm/{module}`.

## Versioning

Orchard Crest UI Framework uses a five-part compatibility version:

```text
{orchard-major}.{orchard-minor}.{orchard-patch}.{crest-security}.{crest-bug}
```

The first three parts identify the Orchard Core version tested with this build. The last two parts are Orchard Crest UI Framework's security and bug-fix counters.

Current compatibility version: `3.0.0.0.0`.
