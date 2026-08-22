# Localization

How Crest resolves and applies culture (language, date/number formatting) for users,
and how UI strings are translated. This covers the user-facing localization system —
per-user language, culture-aware formatting, translated menus/components/content. It
deliberately excludes anything ERP-specific (currency, tax, regions, business
documents); that lives in the host's business/ERP design (fruitful:
`plans/business-regions-and-accounting.md`).

## Scope and boundary

This is an **optional, additive feature set** layered on top of OrchardCore's standard
localization module — nothing else in the codebase requires it. If it were stripped out
entirely, other modules keep working correctly using only stock OrchardCore localization
(tenant culture); they don't get a required new dependency, cascading parameter, or
interface to implement. The per-user stored default and the client-resolved cookie are
enhancements, not a new baseline.

Localized validation *messages* (translated text like "This field is required") go
through the same string-localization pipeline as any other UI text. This is distinct
from *which validation rule ran* (e.g. postal code format, allowed currency) — rule
definitions are a business-context concern, not part of this system.

## Resolution architecture

**The Blazor WASM client is the source of truth for which culture to display and which
culture to tell the server about.** The server does not guess culture from a chain of
independent `IRequestCultureProvider`s — that approach (multiple providers racing inside
`RequestLocalizationMiddleware`) was tried and abandoned: two upstream providers
(`AdminCookieCultureProvider`, `UserLocalizationRequestCultureProvider`) both prepend
themselves via `AddInitialRequestCultureProvider`, so whichever module's
`ConfigureServices` ran last silently won — determined by module load order, not
anything a site admin controls.

Instead:

1. **Client fetches inputs** on manifest load: the tenant's supported cultures + tenant
   default culture, and the signed-in user's stored default culture, all surfaced
   through the manifest.
2. **Client resolves**, in priority order:
   1. **Session override** — a culture the user explicitly picked in the titlebar
      culture picker this browser tab/session. Never persisted as the stored default
      unless "Save as default" is clicked.
   2. **User's stored default** — from `UserLocalizationSettings.Culture`, read via the
      manifest as the currently authenticated user (correct per-identity automatically).
   3. **Admin default culture** — a Crest-owned tenant setting
      (`CrestLocalizationSettings.AdminDefaultCulture`), consulted only when the current
      route is under the admin path prefix; skipped when unset or off-admin.
   4. **Browser locale** (`navigator.language`) — used only if it's one of the tenant's
      supported cultures.
   5. **Tenant default culture** — final fallback.
3. **Client writes one cookie** with the fully-resolved value — Crest's own
   `crest_culture_{shellVersionId}` — in the stock ASP.NET Core cookie format
   (`c=<culture>|uic=<culture>`), scoped tenant-wide (not `/admin`-only). The stock
   `CookieRequestCultureProvider` reads it back server-side.
4. **Server reads back exactly what the client decided.** No ordering race remains,
   since there's a single writer by construction. `UserLocalizationRequestCultureProvider`
   and `AdminCookieCultureProvider` stay registered (other code may expect
   `OrchardCore.Users.Localization` to be on) but neither is relied on to resolve
   anything for Crest.

### Per-tab and per-user override scoping

The session override (priority rung 1) is stored in `sessionStorage`, not
`localStorage` — `localStorage` is shared across every tab of the same origin, which
would break the requirement that an admin tab and a front-end tab can independently show
different languages at once. Because a cookie is per-origin (not per-tab), the client
re-resolves and rewrites the culture cookie **immediately before every
culture-sensitive request**, not just on manifest load — implemented in
`CrestAntiforgeryHandler`, the single `DelegatingHandler` every Crest API call already
funnels through.

The override key is also scoped by user name (`crest-culture-override:{userName}`), so
switching identities in the same tab ("sign in as another user") doesn't leak one user's
override onto another — the new identity falls through to its own stored default.

A same-origin link opened in a new tab (`target="_blank"`, `window.open`) inherits the
source tab's override for free, because browsers clone `sessionStorage` into the new tab
at open time — this is standard spec'd behavior, distinct from an independently-opened
tab (typed URL, bookmark) which correctly starts blank.

### Legacy/iframe pages

`LegacyAdminFrame.razor` embeds legacy Orchard admin pages same-origin, same path
prefix. Because the culture cookie is tenant-wide (not `/admin`-scoped) and the iframe is
same-site, the browser sends the cookie automatically on the iframe's own request — no
extra plumbing needed.

## Front end and anonymous visitors (server-rendered)

The tenant's front-end site (`OrchardCore.Crest.Site`) is plain server-rendered
Razor/Liquid — no WASM client to resolve anything itself. It relies on the stock ASP.NET
Core `RequestLocalizationOptions` pipeline instead:

1. `CrestCultureCookieOptionsConfiguration` (`OrchardCore.Crest.Server/Services/CrestCultureCookie.cs`)
   rebuilds `RequestCultureProviders` as
   `[CookieRequestCultureProvider, AcceptLanguageHeaderRequestCultureProvider]` —
   Crest's own tenant-wide cookie (the same one the admin's client-side chain writes)
   first, then the browser's `Accept-Language` header as the fallback for a visitor who
   hasn't run the admin client yet.
2. This runs as an `IPostConfigureOptions<RequestLocalizationOptions>`, not
   `IConfigureOptions<T>`, deliberately: stock OrchardCore.Localization's
   `AdminCookieCultureProvider` also inserts itself into this same options object via its
   own `IConfigureOptions<T>`, and ASP.NET Core does not guarantee configure-delegate
   ordering across independent DI registrations — two competing `Insert(0, ...)` calls
   race, and whichever ran last would win unpredictably. `IPostConfigureOptions<T>` is
   guaranteed to run after every `IConfigureOptions<T>`, so this wins deterministically
   instead of fighting the race.
3. The tenant's actual supported/default cultures come from `LocalizationSettings`
   (`OrchardCore.Localization`'s site settings, editable at `/Admin/Settings/localization`
   or via a recipe's `settings` step) — **as top-level keys of the step itself**
   (`LocalizationSettings`, not wrapped in an extra `Properties` key). `SettingsStep.cs`'s
   recipe handler writes any key it doesn't special-case directly into
   `site.Properties[key]`, so an extra wrapper key lands one level too deep and
   `GetOrCreate<T>`'s lookup silently returns an empty settings object instead of
   erroring — no exception, just cultures that quietly never resolve.

An anonymous visitor with no cookie yet gets whatever their browser sends via
`Accept-Language`, falling back to the tenant's `DefaultCulture` if their locale isn't in
`SupportedCultures`.

## Culture-aware date/number formatting

`DisplayManager` assigns the resolved culture to `CultureInfo.CurrentCulture`,
`CurrentUICulture`, `DefaultThreadCurrentCulture`, and `DefaultThreadCurrentUICulture` in
the WASM process whenever it re-resolves culture. Every ambient-culture format call
(data grid columns, date pickers, schedulers, treemaps) then formats correctly without
needing to be threaded the culture explicitly. `BlazorWebAssemblyLoadAllGlobalizationData`
is enabled in the Admin WASM project so non-default cultures get full ICU formatting
data (the SDK's default trimmed set only covers a handful of cultures).

## Storage

- **Tenant default culture / supported cultures** — stock OrchardCore
  `LocalizationSettings`, managed on `Pages/Localization.razor`.
- **Admin default culture** — Crest-owned `CrestLocalizationSettings.AdminDefaultCulture`,
  managed on the same page. Nullable; clearing it (or removing the culture it points at
  from the supported-cultures list) means "no admin-specific override, use the tenant
  default everywhere."
- **Per-user stored default** — `UserLocalizationSettings.Culture` (upstream
  `OrchardCore.Users.Localization`'s storage, via `User.Properties`). Read/write via
  `GET`/`PUT api/crest/localization/me`, scoped to the current user.
- **Session override** — client-only, `sessionStorage`, keyed by user name. Never touches
  the server unless explicitly saved as the new stored default.

## UI string localization

Two independent mechanisms, by project:

- **`OrchardCore.Crest.Components`, `OrchardCore.Crest.Site`** — compiled-resource /
  `.po`-based mechanisms (`CrestStrings.resx` + `ILocalizer`/`Localizer` for Components;
  a `.po`-based theme mechanism for Site). New languages are `.po`/API content, not new
  `CrestStrings.{culture}.resx` files.
- **`OrchardCore.Crest.Admin` (headless Blazor WASM) and any project that can reference
  `Crest.Components`** — pulls its string catalog at runtime via an API rather than
  compiled resources:
  - Keys are **invariant literals**, native Orchard style: `T["Some text"]`, or
    `T["Signed in as {0}", name]` for format strings. The literal is simultaneously the
    translation key and the rendered fallback — there is no separate slug key. Because
    the same literal is the `msgid` in every shipped module catalog, Crest pages inherit
    upstream translations automatically. (Identity is a separate concern: menu items keep
    `UniqueId` for manipulation; literals are only ever translation keys.)
  - **Literal fidelity**: lookups are case-sensitive (`Ordinal`), matching OrchardCore's
    own msgid behavior — deliberately not loosened. A literal only inherits an upstream
    translation when it matches upstream's msgid **byte-for-byte** (casing, punctuation,
    trailing periods). When a Crest literal drifts from a stock string, fix the literal
    to match stock; never make matching fuzzier. Upstream msgids are mechanically
    extracted from their source, so the authoritative spelling is whatever their code
    says (`Log in`, not `Login`; `Disable local password login.` with the period).
  - Server: `GET api/crest/localization/strings?culture={culture}` on
    `CrestLocalizationController` serves a **layered** per-culture dictionary, callable
    pre-login, resolving each literal as **stored edit → PO → miss** (the client then
    renders the literal): tenant translation store entries under context
    `Crest.Admin.Client` first; then PO — a `Crest.Admin.Client`-context entry if one
    exists, else the most common translation of the literal across **all** shipped
    catalogs (via `CrestPoTranslationLookup`, the same helper the menu caption chain
    uses). Every layer falls back from a region culture (`es-ES`) to its parent (`es`).
    Storing the literal itself as the value pins the literal over a shipped translation.
  - Client: `CrestApiLocalizer` (`Crest.Components.Theme.CrestApiLocalizer`) implements
    `ILocalizer`, exposes the `T["..."]` indexer, caches per culture, and renders the
    literal itself on a miss rather than throwing. Loaded whenever `DisplayManager`
    resolves a new culture. Convention: `@inject Crest.Components.Theme.CrestApiLocalizer T`.
  - Crest-authored `.po` overrides for `Crest.Admin.Client` live under the owning
    project's own `Localization/{culture}.po` (literal msgids, `msgctxt
    "Crest.Admin.Client"`) — `ModularPoFileLocationProvider` checks each extension's own
    `SubPath` first, so no global `/Localization/{culture}/*.po` entry is needed.

Excluded from the API-based mechanism: `OrchardCore.Crest.Workflows.Designer` (a
vendored third-party package, not a Fruitful admin surface) and any currently-unreferenced
component with no live surface to verify translations against.

## Where translations come from: Crowdin, the mirror, and the private lane

Upstream OrchardCore translates nothing itself — translation is outsourced to volunteers
on **Crowdin** (crowdin.com) via the `OrchardCore.Translations` repo:

1. **Extraction** — `OrchardCoreContrib.PoExtractor` scans upstream source for every
   localizable string (`S["..."]`, `@T["..."]`, data annotations) and generates
   per-project templates: `msgid` = the exact source literal, `msgctxt` = the declaring
   class/view. msgids are therefore byte-exact source text, never editorial.
2. **Translation** — volunteers translate the English strings per language in Crowdin's
   web UI. Untranslated entries stay as empty `msgstr ""`.
3. **Packaging** — exports are packed into the `OrchardCore.Translations.{lang}` /
   `.All` NuGet packages per OrchardCore release.

Two consequences: **coverage lag** (extraction is automatic, translation is human — new
strings sit untranslated until a volunteer does them) and **release lag** (strings added
upstream after the last release don't exist as msgids at all yet).

**The mirror**: the app's root `Localization/` folder is a committed snapshot of that
package's content. Treat it as **read-only upstream output** — never hand-edit it; the
next refresh from the package clobbers local edits. Nothing refreshes it automatically.

**The private lane**: everything Fruitful authors lives in module-local catalogs
(`OrchardCore.Crest.Server/Localization/{culture}.po` etc.) and the tenant translation
store. Crowdin has no knowledge of these in either direction — nothing uploads them,
no refresh touches them. Crest-authored literals (strings that are not upstream msgids)
can **only** be translated here; they never appear in Crowdin's template, so modules
that must stay private lose nothing. This is also why **Crowdin is not a runtime
dependency**: what the app consumes is `.po` files on disk — an air-gapped deployment
localizes identically.

**Where the two worlds meet** — literals that are also upstream msgids (the point of
literal-key alignment). Resolution order arbitrates: store edit → own `.po` entry
(`Crest.Admin.Client` context) → upstream catalog → the literal. A Crowdin refresh can
only change what renders when layers 1–2 say nothing about a string — the desirable
case, where never-touched gaps quietly gain translations. Adding your own entry
permanently shadows upstream's version, and pin-to-literal (store the literal as the
value) vetoes a shipped translation outright.

Every fruitful literal therefore falls into one of three populations (audit sweep,
2026-08-22): **inheriting** (matches a translated upstream msgid), **Crowdin
candidates** (matches an untranslated upstream msgid — translating it on Crowdin's
OrchardCore project benefits everyone and flows in at the next Translations release),
or **Crest-authored** (private lane only).

## Menus and navigation

Admin menu captions are **tenant data**, resolved per request culture at serialization
time by `CrestMenuCaptionResolver` through the layered hierarchy **store edit → PO
translation → invariant literal** (within the store: exact menu context → parent
contexts → best entry in the culture; within PO: `*.AdminMenu` contexts preferred, then
most-common), with provider-item translations seeded from the PO catalogs at import and
edited in the Crest translations page (`/Admin/DataLocalization`). Deleting a stored
entry reverts to the next layer down. `docs/localization.mmd` is the full resolution
chain as a diagram — rename overlay, store steps, PO tiers, literal — kept current with
the resolver. `docs/AdminMenu.md` documents the whole system — the provider-menu
import, item identity, seeding, the PO layer, the translations editor, and per-culture
renames.

Admin-menu override persistence (hidden/reordered/renamed/re-iconed state) keys items by
`AdminNode.UniqueId` (which the node navigation builders copy onto `MenuItem.Id`), with
the invariant `Text.Name` literal as the fallback for items no node backs — never by the
resolved display text, which varies per culture. Renames are recorded per culture; see
`docs/AdminMenu.md`'s "Renames are per culture".

The user profile dropdown menu is built from native, admin-editable `AdminMenu`
documents (`CrestMenuPlacement.User`), merged across every enabled menu of that
placement the current user has permission to see, the same way OrchardCore merges its
own admin sidebar.

## CMS content

Localized page/content-item text goes through stock Orchard Core Content Localization —
unchanged by anything in this document.

## Notice: server machine culture and invariant globalization

`LocalizationService.cs`'s own fallback — used only when a tenant has *no*
`LocalizationSettings` document at all — is `[CultureInfo.InstalledUICulture.Name]`,
i.e. the host machine's configured locale. In a container with `LANG=C.UTF-8` (no named
.NET culture), `CultureInfo.InstalledUICulture` resolves to `""` (effectively
`CultureInfo.InvariantCulture`), and that empty value silently becomes the tenant's
"supported culture" whenever real settings are missing — not a thrown error, just every
visitor getting invariant/base-key formatting regardless of `Accept-Language`. This is
unrelated to ICU data being present (`CultureInfo.GetCultureInfo("es-ES")` resolves fine
even with `LANG=C.UTF-8`); it's specifically that `InstalledUICulture` has nothing to
map the OS locale name to. fruitful's `dev/dev.sh` and `dev/reference-sample.sh` both
pin `LANG=en_US.UTF-8` for local dev to avoid this trap — if you ever see anonymous
visitors stuck on invariant formatting in a fresh environment (a new container image, a
CI runner, a minimal deployment target) with otherwise-correct `LocalizationSettings`,
check `LANG` on that host before assuming it's a code bug.

## Testing

- **C# unit tests** exist per `OrchardCore.Crest.*` subproject
  (`OrchardCore.Crest.Server.Tests`, `.Admin.Tests`, etc.), following the
  `OrchardCore.Crest.Icons.Tests` convention (xUnit + Verify + NSubstitute), discovered
  automatically by `tests/run-tests.sh`. Localization-specific coverage: the culture
  resolution priority chain (table-driven over all rungs and combinations),
  `IsUnderAdminPath()`, `CultureSelector.FromAsync`, and `LocalizationController.SaveAsync`'s
  validation of `AdminDefaultCulture`.
- **Playwright checks** under `tests/playwright/checks/`, wired into `run-admin-suite.js`
  and `run-client-suite.js`, cover: sequential settings changes (tenant → admin → user →
  session override, each checked in order), per-tab override scoping, same-origin
  new-tab override inheritance, multi-user switch (no override leakage between
  identities), anonymous resolution (enabled vs. unsupported browser locale), and a
  front-end translation smoke test. A hidden `data-testid="resolved-culture"` hook on
  `AdminTitleBar.razor` is the DOM signal these checks assert against, since the visible
  culture label is an optional tenant setting.
- Tests that mutate tenant/user settings reset that state afterward so they don't leave
  the dev tenant in a non-English state for unrelated manual testing.
