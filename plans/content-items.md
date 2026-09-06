# Content system — open work and questions

Companion to [docs/Content-Items.md](../docs/Content-Items.md), which explains
what EXISTS. This file holds only what is not built yet, plus standing open
questions. Nothing here is scheduled unless stated.

## Deferred by ruling (do not build without a new decision)

- **Caller-supplied query narrowing** (the third filter axis): deferred because
  static filters cover the motivating cases. If ever built it must be
  NARROWING-ONLY — intersected with the attachment's configured filters, never
  able to widen them (the query endpoint's no-client-filters rule stands).
- **`ListsAdminNode`-style item-per-container-instance menus** (each Blog as its
  own entry): out of scope. If wanted, it is a SECOND navigation provider, not a
  change to `ContentTypesMenuNavigationProvider`.
- **Real cross-table partitioning by year/open-closed** for picker indexes:
  YesSql will not route queries across partitions, so the query layer would have
  to. The snapshot rule (posted documents are closed records) keeps the door
  open.
- **Text-filter SQL promotion**: accepted boundary — in-memory matching is
  case-insensitive, SQL equality follows DB collation; the bounded-oversample
  residual path stays until someone accepts a semantics change.

## Not built yet

- **Per-type designed pages.** Every menu-flagged type lands on the generic
  content-items surface. The Blazor designer is the intended replacement, per
  type, mounting at the same menu entry — the menu mechanism needs no change.
- **Guided creation of option content types.** Lists can be ASSIGNED an option
  type (dropdown on the list header), but creating one still means visiting the
  Content Types screen, creating a type carrying `CrestOptionPart`, and coming
  back. A create-inline flow is a UX nicety, not machinery.
- **Rename-safety for synced menu entries.** The sync match key is parent path +
  invariant caption (= the type's DisplayName), so a display-name rename re-keys
  the entry and orphans layout overrides, and two types sharing a DisplayName
  collide. Acceptable today (stock has the same property); a UniqueId-based
  match key would harden it if it ever bites.
- **Worldwide subdivisions** (~5,000 ISO 3166-2 entries) are deliberately not
  seeded. If a tenant needs them at scale, that is the worked example for
  promoting one list to a `contentitem` source (see the docs' promotion gotcha).
- **NANP-wide area codes** (Canada/Caribbean): the seeded list is US +
  territories by ruling; widening is a separate decision. Overlay codes are
  added a few times a year — adds-only reseeds are the update path.
- **Per-tenant/per-user preferred-unit settings.**
  `MeasurementPreferenceService` + `uom/preferences` resolve culture → units;
  the per-tenant/per-user adaptation layers on that seam later.

## Backlog — Blazor / Liquid / Razor parity

Deferred PENDING A FULL AUDIT; recorded so findings are not lost. The audit
decides which parity refactors happen — nothing below is scheduled.

Context: Crest extends Orchard's rendering stack rather than replacing it, so
"can a non-Blazor surface use what Crest builds?" has real answers today.

- **Liquid can already embed Crest components.**
  `CrestBlazorComponentShapeBindingResolver` participates in Orchard's shape
  pipeline the way `OrchardCore.Templates`' resolver does, so
  `{{ "ComponentName" | shape_new: text: "..." | shape_render }}` renders a
  Blazor component via `HtmlRenderer`. Named arguments land in
  `IShape.Properties` and map onto `[Parameter]`s by name. Falls through for
  unregistered shape names. STATIC SSR ONLY — no hydration, no `@rendermode`.
- **Liquid cannot call the `api/crest/*` endpoints, and should not.** Liquid
  renders server-side against objects in scope; an HTTP request back into the
  same server to reach a service already in DI is the wrong shape.
- **The right mechanism is `AddLiquidFilter` over the SAME services the API
  wraps.** Prior art: `OrchardCore.Users` (`users_by_id`, `has_permission`,
  `is_in_role`) calling `IUserService` directly. A Crest equivalent —
  `option_list`, `option_label` over `ICrestContentPartListService` — gives
  template authors `{{ item.Content.TransactionPart.Status | option_label }}`
  with no Blazor involved. Small: a few filters over an existing service.
- **`option_label` is not a nicety.** Content stores `ContentItemId`, code
  matches `Key`, humans read `DisplayText`; a naive `{{ ...Status }}` renders an
  opaque id. Without a label filter, template authors will hardcode id
  literals — exactly the failure the identity rule exists to prevent.
- **Liquid filters BYPASS controller authorization.** The controller checks
  `CanViewAsync`; a filter calling the service directly checks nothing unless
  written to. Low stakes for content part lists, but the same pattern applied to
  a `customers` filter reproduces the data-scope hole on the PUBLIC side, where
  templates render for anonymous visitors. Orchard shipping
  `has_permission`/`is_in_role` suggests template-level authorization is
  expected to be EXPLICIT.
- **Tenant-authored Liquid is a growing security surface.** `TitlePattern` is
  itself tenant-authored Liquid, so Fluid sandboxing (what object graph a
  template can reach) belongs with the data-scope work, not treated separately.
- Scope note: all of this concerns the Orchard CMS surface (public pages,
  shapes, templates). It does NOT extend to admin/ERP screens — those are Crest
  Blazor endpoints only, by standing ruling, with no Liquid path in.

## Known transients / test-suite notes

- The once-per-shell provider-menu sync can race checks run immediately after
  fresh provisioning (a dashboard-screenshot diff, a menu fetch error). Settled
  re-runs pass. Do not rebaseline a screenshot for a first-run diff.
- `content-type-menu-toggle` flips a real setting through the API; a mid-check
  crash can leave the flag set. Inherent to testing real state; the check is
  API-driven to keep the window small.
