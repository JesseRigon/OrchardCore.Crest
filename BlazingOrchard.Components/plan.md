# BlazingOrchard Remaining Work

This file tracks work that is not implemented yet. The current architecture overview lives in `../README.md`.

## Neutral Client/Core Package

- [ ] Create a UI-library-neutral client package, likely:

  ```text
  BlazingOrchard.Client/
    Context/
    Display/
    LegacyFrame/
    Models/
  ```

- [ ] Move client-safe DTO contracts out of `BlazingOrchard.Components` when they are not Radzen-specific.
- [ ] Move shared shape/model contracts into the neutral client package.
- [ ] Keep the neutral client package free of Radzen, Orchard server assemblies, MVC, Razor Pages, and `Microsoft.AspNetCore.App` server-only dependencies.
- [ ] Update `BlazingOrchard.Components` and module WASM projects to reference the neutral client package after extraction.

## Display Management

- [ ] Define `IBlazingDisplayManager`.
- [ ] Define `BlazingDisplayManager`.
- [ ] Define `BlazingDisplayDriver<TModel>`.
- [ ] Define `BlazingDisplayContext`.
- [ ] Define `BlazingDisplayResult`.
- [ ] Define `BlazingPlacementInfo`.
- [ ] Decide whether the current admin WASM `DisplayManager` should be split into:
  - neutral display/rendering orchestration, and
  - admin-shell state/session/theme service.
- [ ] Move neutral display concepts out of `Themes/BlazingOrchard.Admin/wasm/DisplayManagement`.
- [ ] Keep Radzen renderers in `BlazingOrchard.Components`, not in the neutral display package.
- [ ] Add concrete Radzen field renderers, for example:

  ```text
  BlazingOrchard.Components/Components/Display/RadzenModelEditor.razor
  BlazingOrchard.Components/Components/Display/Fields/RadzenTextFieldRenderer.razor
  BlazingOrchard.Components/Components/Display/Fields/RadzenNumericFieldRenderer.razor
  BlazingOrchard.Components/Components/Display/Fields/RadzenBooleanFieldRenderer.razor
  BlazingOrchard.Components/Components/Display/Fields/RadzenContentPickerFieldRenderer.razor
  ```

## Legacy Frame Client Abstraction

- [ ] Move iframe URL-building behavior out of the Radzen admin shell into a neutral client package.
- [ ] Create a reusable neutral component or service set, for example:

  ```text
  BlazingOrchard.Client/Components/LegacyFrame.razor
  BlazingOrchard.Client/LegacyFrame/LegacyFrameUrlBuilder.cs
  BlazingOrchard.Client/LegacyFrame/LegacyFrameOptions.cs
  ```

- [ ] Keep `legacy-frame=1` / `legacy-frame=true` as the shared query convention.
- [ ] Allow `BlazingOrchard.Components` to wrap or style the neutral legacy frame component with Radzen-specific chrome.
- [ ] Ensure future component systems can reuse the legacy frame selector, frame theme, URL convention, and client iframe behavior without copying Radzen admin-shell code.

## Module Discovery And Routing

- [ ] Replace or supplement the current build-time WASM project glob with an explicit module registry/manifest model.
- [ ] Define how module-contributed Blazor routes are discovered.
- [ ] Define how module-contributed component assemblies are exposed to the admin shell.
- [ ] Define route collision behavior and precedence.
- [ ] Define how module metadata is represented in the boot manifest.
- [ ] Keep module discovery generic so future component systems can opt into the same conventions.

## API And Contract Cleanup

- [ ] Audit every `api/blazing/*` endpoint against Orchard's native APIs.
- [ ] Replace custom content reads with Orchard Contents REST API or GraphQL where sufficient.
- [ ] Verify whether Orchard Core exposes suitable JSON APIs for:
  - content definitions,
  - roles,
  - site settings,
  - features.
- [ ] Add or verify explicit authorization checks for all admin-level reads and writes.
- [ ] Add antiforgery handling for state-changing Blazing endpoints where needed.
- [ ] Keep `api/blazing/*` endpoints thin: they should call Orchard services, enforce Orchard permissions, and return Blazor-friendly JSON without owning duplicate state.

## Packaging

- [ ] Decide final NuGet package boundaries:
  - `BlazingOrchard.Server`
  - `BlazingOrchard.Components`
  - `BlazingOrchard.Client`
  - theme packages if needed
- [ ] Set `IsPackable=true` where packages are intended.
- [ ] Add package metadata, descriptions, icons/readmes, repository metadata, and license metadata.
- [ ] Verify package dependency boundaries so `BlazingOrchard.Server` does not depend on Radzen.
- [ ] Verify component packages do not accidentally include generated `bin`/`obj` or unrelated theme source.
- [ ] Decide whether Orchard-loadable themes are packaged independently or bundled with the components package.

## Documentation

- [ ] Keep the top-level `README.md` aligned with the current architecture.
- [ ] Document how a module contributes a Blazor WASM project.
- [ ] Document how a module contributes admin routes.
- [ ] Document how a future non-Radzen component system should integrate.
- [ ] Document the legacy frame URL/query contract.
- [ ] Document the expected API-selection order: Orchard native API, GraphQL, Query API, OpenID/JWT, then thin Blazing adapter.

## Validation

- [ ] `dotnet build Fruitful.csproj --no-restore`
- [ ] `node tests/playwright/model-list-actions.js`
- [ ] `node tests/playwright/admin-route-sidebar.js`
- [ ] `node tests/playwright/contentitems-customer-filter-refresh.js`
- [ ] Add Playwright coverage for legacy frame fallback routes.
- [ ] Add Playwright coverage for module-contributed Blazor routes.

Browser validation should use reusable scripts under `tests/playwright`; avoid inline one-off Playwright scripts.
