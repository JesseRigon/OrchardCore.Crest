# BlazingOrchard

BlazingOrchard is an Orchard Core module/theme package. It plugs into an Orchard host and provides a Blazor-based admin shell, but Orchard remains the authority for tenants, users, permissions, content, features, settings, themes, and navigation.

## Versioning

BlazingOrchard uses a five-part compatibility version:

```text
{orchard-major}.{orchard-minor}.{orchard-patch}.{blazing-security}.{blazing-bug}
```

The first three parts identify the Orchard Core version that this BlazingOrchard build has been tested against. The last two parts are owned by BlazingOrchard.

Examples:

- `3.0.0.0.0` - initial BlazingOrchard release for Orchard Core `3.0.0`.
- `3.0.0.0.1` - BlazingOrchard bug fix for Orchard Core `3.0.0`.
- `3.0.0.1.2` - BlazingOrchard security fix `1` and bug fix `2` for Orchard Core `3.0.0`.
- `3.0.1.2.1` - tested against Orchard Core `3.0.1`, carrying BlazingOrchard security line `2` and bug line `1`.

When the Orchard Core compatibility line moves to a new main release, the BlazingOrchard-owned counters reset to `.0.0`.

Current compatibility version: `3.0.0.0.0`.

The Orchard manifests use the five-part version. .NET assembly and NuGet-compatible package properties keep their required SemVer/assembly-safe forms, with the five-part value stored as `AssemblyInformationalVersion` / `BlazingOrchardVersion`.

## API strategy

Do not build a parallel admin API when Orchard already provides one.

Preferred order for Blazing Admin data access:

1. Use Orchard's built-in JSON/REST APIs when they provide the needed contract.
2. Use Orchard GraphQL for content/query/read models where the GraphQL module already fits.
3. Add or extend Orchard GraphQL schema/mutations when a typed Blazor contract is useful and belongs to Orchard data.
4. Keep a small `api/blazing/*` adapter only when Orchard exposes the behavior as MVC/Razor admin UI, service APIs, or shape/menu builders rather than a stable JSON endpoint.

Any `api/blazing/*` endpoint must be a thin adapter over Orchard services. It must not own duplicate state or bypass Orchard authorization.

```text
Blazing Admin UI
  -> Orchard REST/JSON API when available
  -> Orchard GraphQL for content/query projections
  -> Thin BlazingOrchard adapter only for Blazor-specific projections/actions
       -> calls Orchard services
       -> checks Orchard permissions
       -> returns Blazor-friendly JSON
```

## Controller audit

Current controllers are mostly thin adapters over Orchard services. The main cleanup direction is to remove custom endpoints where a stable Orchard API or GraphQL query can satisfy the Blazor UI.

| Controller | Current purpose | Preferred source | Decision |
| --- | --- | --- | --- |
| `AppController` | Blazor app manifest: tenant, site, admin base path, feature hash, enabled features, admin menu | Orchard services: `ShellSettings`, `IShellDescriptorManager`, `ISiteService`, `INavigationManager` | Keep as Blazor shell adapter. No single stock Orchard API provides this combined app manifest. |
| `BlazingAuthController` | JSON `me`, login, logout for Blazor | Orchard Users/authentication | Keep only as a Blazor auth adapter unless a stock JSON login/session endpoint is enabled and suitable. Must continue to use Orchard user services/cookies. |
| `BlazingThemeController` | Module-specific Radzen/theme token settings from site properties | Orchard site settings | Keep while these settings are Blazing-specific. If the settings become a normal Orchard settings section with a standard API, switch to that. |
| `ContentItemsController` | Read content item by handle | Prefer Orchard GraphQL or Orchard Contents API | Candidate for replacement. Blazor content reads should use GraphQL/standard content APIs where possible. Keep only as temporary compatibility or for a missing by-handle projection. |
| `ContentTypesController` | List/read content type definitions | Orchard content definition services; possible GraphQL/schema projection | Audit further before expanding. Keep as a thin read adapter only if Orchard has no stable JSON content-definition endpoint for the needed UI. |
| `FeaturesController` | List enabled features | Orchard Features admin/services | Keep as read-only adapter for Blazor if stock feature admin remains MVC/Razor-only. Any enable/disable action should use Orchard permissions and services, not custom state. |
| `NavigationController` | Build admin or named menus as JSON | `INavigationManager` | Keep. Orchard navigation is built through menu services/shapes; Blazor needs a JSON projection of the resolved menu. |
| `RolesController` | List roles | Orchard roles services/admin | Keep as read-only adapter only if no stock JSON role endpoint is available. Must enforce Orchard role-management permissions before mutating or exposing admin-only role details. |
| `SiteController` | Read/update site settings | Orchard settings services/admin | Keep only as a settings adapter if stock settings APIs are not suitable. Must enforce Orchard settings permissions for reads/writes. |
| `ThemesController` | List/select/enable/disable site/admin themes | Orchard themes services/admin | Keep. Orchard's standard Themes admin is MVC/Razor; this adapter mirrors it as JSON and already checks `OrchardCore.Themes.Permissions.ApplyTheme`. |

## Rules for adding endpoints

Before adding a new `api/blazing/*` endpoint:

1. Check whether Orchard already exposes a JSON/REST API for the feature.
2. Check whether GraphQL can query or mutate the data cleanly.
3. If a Blazing endpoint is still needed, keep it projection-oriented and call Orchard services directly.
4. Use Orchard permission constants and `IAuthorizationService` for admin-level data/actions.
5. Avoid Fruitful-specific names, tenants, credentials, recipes, or local dev settings in this module.

## Near-term API cleanup

- Replace `ContentItemsController` reads with GraphQL or Orchard Contents API if the enabled Orchard modules provide the required by-handle lookup.
- Verify whether Orchard Core 3.0 exposes stable JSON endpoints for content definitions, roles, site settings, and features in the host configuration.
- Add explicit authorization checks to all admin adapters that expose settings, roles, features, content definitions, or content data.
- Keep `ThemesController` as the pattern for Blazor admin adapters: Orchard service calls plus Orchard permission checks, returning only UI-friendly JSON.
