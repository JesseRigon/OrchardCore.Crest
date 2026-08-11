# Agent Notes

## General Instructions

- Read all README files and all `agents.md`/`AGENTS.md` files in this repo before making substantial changes.
- Do not use one-off inline/terminal-coded Playwright scripts for browser validation.
- Tests live with their owning project under `<project>/tests/`. Save new tests in that project's `tests/` directory.

## Orchard Integration

- Prefer native Orchard APIs before adding `api/crest/*`: Contents REST, GraphQL, Query API, OpenID/JWT, Media, Taxonomies, Users, and existing admin services/controllers.
- Keep Crest server thin: JSON adapters over Orchard services only; do not duplicate Orchard's content-definition, permissions, display-driver, or API framework.
- Treat Orchard as authoritative for tenant scope, permissions, feature gates, validation, lifecycle, and provider extensibility. Crest may adapt these for Blazor but must not bypass, duplicate, broaden, or persist parallel state; use provider-neutral Orchard abstractions in shared pages.
- Store tenant-scoped Crest settings in Orchard site settings whenever possible. Use a cross-tenant or hardcoded setting only when explicitly requested, and prefer Orchard's standard configuration/settings patterns for that scope.
- Crest JSON adapters must use `ICrestRequestAccess`: authorize the real Orchard request principal with the native permission/resource before resolving a domain service. Unsafe calls use Orchard antiforgery validation; do not add Crest-local ACLs or role checks.
- Crest wrappers are for gaps where Orchard exposes MVC/Razor admin UI, server services, or shapes/menus instead of stable Blazor-friendly JSON.
- No literal path strings in Crest.Admin, Crest.Site, Crest.Components, or any submodule — anywhere — unless that literal is the actual place registering the endpoint with Orchard (an `IPostConfigureOptions`-sourced value like `BlazorAdminThemeOptions.AdminPath`/`LoginPath`, or a route pattern read from Orchard config). If a path needs comparing, gating, or rewriting anywhere else, it must be sourced from that one registration point, never re-typed as a second string constant. This applies to Blazor `@page` directives too when their value is used for anything beyond Blazor's own internal component matching (e.g. compared against in middleware, or read back by other code) — invented path constants for internal-only handoff (a "safe landing page" route, a "not served here" sentinel) are exactly what this rule prohibits; find the value from Orchard's own config/registration instead of making one up. **This is not Crest-specific — it applies to all routing in all submodules of this host.** All routing must go through Orchard's own systems (`AutoRoutePart`, tenant/site settings, `IPostConfigureOptions`-sourced config) so that `ShellContext` per-tenant scoping, feature gates, and shell rebuilds keep working correctly — routing built outside Orchard's system silently breaks tenant scoping, not just style.

## UI Notes

- Always validate the specific Blazor/admin page being changed with Playwright before calling the work done.
- Client-side Crest DisplayManager/rendering logic may duplicate Orchard templating concepts because WASM Blazor rendering is separate from Orchard Razor/Liquid shapes.
- Shared Crest components should not depend on Admin/Site or Radzen token names. Expose style parameters/local Crest variables; themes/design systems feed concrete token values into those slots.
- Admin menu items should declare icons explicitly with the Crest convention `.AddClass("icon-class-...")`, using canonical Crest/Iconify provider references such as `.AddClass("icon-class-@iconify:mdi:account-group")`. Do not add raw Font Awesome compatibility classes; legacy third-party menu icons should be handled through admin icon overrides.
- Do not use fallback dictionaries or hardcoded menu-class-to-icon mappings for icons; use real source icon lists/metadata, cached locally when needed.
