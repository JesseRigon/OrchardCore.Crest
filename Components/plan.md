# BlazingOrchard Components Final State

## System overview

BlazingOrchard is split into separately packable projects inside one git repository.

```text
modules/BlazingOrchard.OrchardCoreModule/
  BlazingOrchard.Server/
  Components/
```

`BlazingOrchard.Server` is the Orchard-side runtime. It discovers and serves Blazor component apps contributed by modules, exposes Blazor/headless-friendly JSON APIs over Orchard services, and owns the neutral contracts used by served Blazor clients.

`Components` contains the project-default `BlazingOrchard.Components` component/theme library. It is Radzen-based and contains concrete Blazor UI components, renderers, theme packs, CSS, JavaScript, and static assets used by the admin shell and module UI.

Other modules, such as CRM, explicitly reference `BlazingOrchard.Components` and build their UI against it.

## BlazingOrchard server responsibilities

`BlazingOrchard.Server` owns the runtime and neutral client contract surface:

- Orchard Core module/runtime integration.
- Discovery and serving of module-contributed Blazor apps/components.
- `api/blazing/*` thin wrappers over Orchard services where native Orchard APIs are not enough.
- DTO contracts and model JSON shapes used by Blazing clients.
- Model/content parsing and serialization contracts.
- Content/model metadata envelopes over Orchard definitions/settings.
- Client boot manifests, route registries, module registries, and shell context.
- Client context contracts and services independent of a concrete UI library.
- Neutral DisplayManager contracts/core if they can remain UI-library independent.
- Legacy admin frame routing, query conventions, theme selection, and a reusable client-safe legacy frame abstraction.
- Orchard permissions, authorization, antiforgery, and tenancy integration.

The server does not depend on Radzen and does not contain Radzen controls, Radzen CSS/JS, or Radzen-specific theme implementation.

## BlazingOrchard.Components responsibilities

`Components` owns the concrete Radzen-based `BlazingOrchard.Components` UI implementation:

- Radzen-based reusable Razor components.
- Radzen model list, editor, form, and input components.
- Radzen field renderers/drivers for Blazing model metadata.
- Admin shell UI components when they are concrete UI implementation.
- Color picker and other reusable Radzen input components.
- Blazing admin/site theme packs.
- Component-library CSS, JavaScript, static assets, and visual styling.
- NuGet-packable component/theme library output.

The component library consumes Blazing server/client contracts and renders them with Radzen components.

## Module dependency rule

Module UI projects declare the component library they are built against.

For the current application line:

```text
CRM.BlazorWasm -> BlazingOrchard.Components
BlazingOrchard.Admin.Wasm -> BlazingOrchard.Components
```

This makes module UI Radzen-based. A different component system would require module UI to reference and implement against that component library explicitly.

## DisplayManager final direction

The Blazing DisplayManager concept is neutral and should model Orchard's display-management architecture while operating over JSON/client DTOs.

Orchard's display manager is conceptually neutral: it builds a tree of shapes/zones through display drivers and placement. Razor and Liquid are later rendering layers over Orchard shapes. Blazing should mirror the pattern client-side rather than directly depend on Orchard server assemblies.

| Orchard server concept | Blazing client concept |
| --- | --- |
| `IContentItemDisplayManager` | `IBlazingDisplayManager` |
| `DisplayDriver<TModel>` | `BlazingDisplayDriver<TModel>` |
| `ShapeResult` | `BlazingDisplayResult` / component descriptor result |
| `IShape` / `Shape` | `BlazingModel` / `BlazingShape` / JSON-backed descriptor |
| `ZoneHolding` | `BlazingZone` |
| `PlacementInfo` | `BlazingPlacementInfo` |
| Shape template rendering | Blazor component rendering |
| Server `ContentItem` object | JSON `BlazingContentItem` DTO |

The base DisplayManager should live in neutral Blazing client/core code, either isolated under `BlazingOrchard.Server` or in a client-safe package such as:

```text
modules/BlazingOrchard.OrchardCoreModule/BlazingOrchard.Client
  Display/IBlazingDisplayManager.cs
  Display/BlazingDisplayManager.cs
  Display/BlazingDisplayDriver.cs
  Display/BlazingDisplayContext.cs
  Display/BlazingDisplayResult.cs
  Display/BlazingPlacementInfo.cs
  Models/BlazingModel.cs
  Models/BlazingContentItemRequests.cs
  Context/BlazingClientContext.cs
```

`BlazingOrchard.Components` provides the concrete Radzen display drivers/renderers that plug into this neutral DisplayManager.

Example concrete component library structure:

```text
modules/BlazingOrchard.OrchardCoreModule/Components
  Components/Inputs/BlazingColorPicker.razor
  Components/Model/BlazingModelList.razor
  Components/Model/BlazingContentItemEditor.razor
  Components/Display/RadzenModelEditor.razor
  Components/Display/Fields/RadzenTextFieldRenderer.razor
  Components/Display/Fields/RadzenNumericFieldRenderer.razor
  Components/Display/Fields/RadzenBooleanFieldRenderer.razor
  Components/Display/Fields/RadzenContentPickerFieldRenderer.razor
```

## Orchard display-management reuse boundary

Direct Orchard display-management assemblies are not used directly in WASM/client code because they depend on server-side Orchard infrastructure:

- `IShape`, `IShapeFactory`, `Shape`, and `ZoneHolding`.
- Shape placement providers and server placement resolution.
- `IUpdateModel` and Orchard model binding.
- Orchard content definitions/services.
- Content part/field activators and display handlers.
- `Microsoft.AspNetCore.App` framework references.

Blazing reuses the architecture, naming patterns, and concepts, but implements a JSON/client-safe analog for Blazor clients.

## Theme and Legacy Frame final layout

Concrete Blazing theme implementation belongs to the component/theme library:

```text
modules/BlazingOrchard.OrchardCoreModule/Components/Themes/BlazingOrchard.Admin
modules/BlazingOrchard.OrchardCoreModule/Components/Themes/BlazingOrchard.Admin/wasm
modules/BlazingOrchard.OrchardCoreModule/Components/Themes/BlazingOrchard.Site
```

These theme projects remain Orchard-loadable projects/modules while being source-owned and packaged with `BlazingOrchard.Components`.

Legacy framing is shared Blazing core infrastructure, not a Radzen/component-library concern. The Orchard-side pieces live with the shared server project:

```text
modules/BlazingOrchard.OrchardCoreModule/BlazingOrchard.Server/LegacyFrameThemeSelector.cs
modules/BlazingOrchard.OrchardCoreModule/BlazingOrchard.Server/Themes/BlazingOrchard.LegacyFrame
```

`BlazingOrchard.LegacyFrame` is the generic stripped Orchard admin theme used to render normal Orchard admin pages inside a Blazor shell iframe. It should stay UI-library neutral and depend only on Orchard admin/display/resource infrastructure. Future Blazor component systems should reuse this theme selector, query convention, and frame theme instead of rebuilding their own legacy-frame pipeline.

The client-side iframe UI should also be neutral when it graduates from the current Radzen admin shell. The target shape is a client-safe Blazing core component or package, for example:

```text
modules/BlazingOrchard.OrchardCoreModule/BlazingOrchard.Client
  Components/LegacyFrame.razor
  LegacyFrame/LegacyFrameUrlBuilder.cs
  LegacyFrame/LegacyFrameOptions.cs
```

Concrete component libraries can wrap or style that neutral component, but the URL/query contract and iframe behavior remain shared Blazing core behavior.

## API strategy alignment

Blazing server does not replace Orchard's API stack. It uses native Orchard APIs first:

1. Orchard Contents REST API for content CRUD when sufficient.
2. Orchard GraphQL for content/query/read models.
3. Orchard Query API for configured queries and reports.
4. Orchard OpenID/JWT for external headless clients.
5. Thin `api/blazing/*` adapters only for Blazor-specific projections/actions or Orchard functionality exposed only as MVC/Razor UI, services, or shapes.

## Validation checklist

- `dotnet build Fruitful.csproj --no-restore`
- `node tests/playwright/model-list-actions.js`
- `node tests/playwright/admin-route-sidebar.js`
- `node tests/playwright/contentitems-customer-filter-refresh.js`

Browser validation uses reusable scripts under `tests/playwright`; do not use inline one-off Playwright scripts.
