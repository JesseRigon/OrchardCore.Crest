# Admin menu layout

Crest treats the built-in Orchard admin menu as a generated primary navigation. Tenant node/order/icon/separator changes made in the Admin Menus page are stored in the tenant document store as layout overrides.

Primary navigation component settings, such as collapse behavior, tier spacing, generated separators, and tier backgrounds, are tenant site settings. They are edited from the same Admin Menus page for UX, but they are not part of the menu layout overlay JSON.

## Recipe import

Hosts can import primary navigation overrides from a recipe with the `CrestAdminMenuLayout` step:

```json
{
  "name": "CrestAdminMenuLayout",
  "file": "crest-admin-menu-layout.json"
}
```

The `file` value is resolved relative to the recipe file. Crest registers this recipe step, but it does not add the step to any host recipe automatically.

## UI export

The Admin Menus page shows an `Export JSON` button on the built-in Primary Navigation. The button exports the current tenant layout to:

```text
<host content root>/recipes/crest-admin-menu-layout.json
```

In a normal source checkout, the host content root is the host app repository folder, so the default export lands in the host app's `recipes` folder and can be versioned with the host recipe.

The export endpoint is enabled automatically in `Development`. In other environments, the host must opt in:

```json
{
  "Crest": {
    "AdminMenuLayoutExport": {
      "Enabled": true
    }
  }
}
```

The endpoint still requires the current user to have Orchard's admin menu management permission.

## Provider items are imported into the admin menu system

Menu items contributed by a module's `INavigationProvider` are materialized as DB-backed
`AdminNode`s in an admin menu named **Primary Navigation**, so Crest's sidebar and menu editor
only ever deal with admin menu nodes.

The reason is that provider items have no dependable identity or translatability of their own:
20 of the 57 upstream admin menu providers never call `.Id(...)`, those that do use hand-written
slugs unique only by convention, and their captions come from PO files — a deploy-time artifact
no tenant admin can edit. Importing them gives each item a `UniqueId` (which the node navigation
builders copy onto `MenuItem.Id`) and a `MenuName`, which is the context `IDataLocalizer` needs
to hold a tenant-level translation.

Items are matched across runs on `MenuItem.Text.Name` — the invariant `S["..."]` literal (the
localization key, written in English only by convention) that OrchardCore's own
`NavigationManager.Merge` matches on, so it does not vary by culture —
qualified by the item's parent path so identically-captioned items under different parents stay
distinct. The match key lives in Crest's own document, leaving `AdminNode` exactly as OrchardCore
defines it.

**How the imported node wins.** At render time both the provider item and its imported node
contribute to the same "admin" menu, and `NavigationManager.Merge` folds each pair into one item.
Merge's authority rule hands the survivor the *higher-priority* side's values, so each node is
imported at the highest contributing provider's priority plus one — the merged item therefore
carries the node's `UniqueId` as its `Id`, plus the node's caption, icon and permissions, while
authorization, href computation and reduction all still run in the standard pipeline. Nothing is
filtered or post-processed outside Orchard's own machinery.

**Fidelity and self-healing.** The node captures the provider item's resolved URL (`.Action(...)`
targets are resolved through `IUrlHelper` at import), permission names, position, and default icon
(resolved from the legacy icon map at import, since the map's slug/class lookup keys no longer
exist on the merged item). On every resync: a priority that no longer beats its provider is
raised; an empty icon is backfilled; a URL still carrying the last-imported value follows the
provider when the provider's target moves — while a URL the admin edited is never touched.

**The "New" branch is not imported.** Its children are regenerated live from the creatable
content types, and Crest's layout logic locks the branch by exactly the key `new` — it stays
provider-owned by design.

**Translations are imported along with the items.** An imported caption resolves through the
tenant translation store (`IDataLocalizer`), not through the provider's own `S["..."]` PO
resource — so the sync *seeds* that store from the PO catalogs: for every supported culture,
every imported caption whose PO catalog carries a translation gets a store entry. Seeding never
overwrites — a translation the tenant already has (edited in the Translations editor or promoted
from a rename) always wins — and each caption/culture pair is seeded **at most once**: the sync
document tracks every pair it has seeded or found already translated, so a translation an admin
deliberately deleted stays deleted across shell restarts instead of being refilled from PO
(`sync-providers` is the exception — invoking it by hand refills anything missing, which is also
the recovery path for the lossy-save trap below). Adding a supported culture seeds that culture
on the next shell start or `sync-providers` call; a pass with nothing new loads no PO catalogs
at all. PO entries are keyed by
contributing class, which merging erases, so the seed matches the caption across the whole
catalog and takes the most common translation when contexts disagree. The result: every
imported caption is a first-class, per-tenant-editable entry in Orchard's own translations
editor (Configuration → Localization → Translations, under the "Primary Navigation" group),
pre-filled with what PO would have shown. One consequence to know: because seeded values are
tenant data from the moment they are written, a later PO catalog update only reaches
cultures/captions that were never seeded.

Crest's sidebar and app manifest resolve these captions against the tenant translation store
per request culture at serialization time — the same place in the pipeline where TheAdmin's
`NavigationItemText.cshtml` resolves at render time — through `CrestMenuCaptionResolver`,
which fixes two defects a bare `IDataLocalizer` lookup carries:

**Merge drops `MenuName`.** `NavigationManager.Merge` folds a provider item and its imported
node into one; the node's values win via priority, but the *surviving instance* is whichever
came first in provider registration order, and Merge's copy list omits `MenuName`
(fruitful's `plans/upstream-orchard-proposals.md` #7). An item that survived as the provider's
instance would resolve under the generic "Admin Menus" context and miss its stored
translation — per caption, decided by module registration order. The resolver restores the
owning menu from the surviving `Id`, which for a merged pair is the node's `UniqueId`.

**No default context.** Contexts are strict namespaces: a translation stored under one is
invisible to a lookup under any other, and upstream falls back to the invariant literal even
when the culture translates the same caption elsewhere. The resolver walks outward instead —
the exact menu context, then parent contexts by stripping `':'` segments
(`Admin Menus:Primary Navigation` → `Admin Menus`), and finally the culture's best entry for
the caption anywhere in the store (contexts under "Admin Menus" preferred, then the most
common value, ordinally tie-broken). The invariant literal renders only when the request
culture holds no translation of the caption at all; an entry in the exact context always
wins, so pinning a caption in the Translations editor overrides every fallback. Each step
checks the specific culture before its parents (`es-ES`, then `es`).

This subsumes the earlier special-cased "New"-branch fallback: a content type translated in
the Translations editor ("Content Types" group) is simply the best alternative for its New
menu caption, which no "Admin Menus" context translates — one translation covers every
surface a type name appears on.

**The Crest translations editor.** `/Admin/DataLocalization` (and `/Index`) render Crest's own
Blazor translations page instead of the stock one — the routes shadow the stock URLs, so the
imported "Translations" menu link and the Localization page's "Edit translations" button land
on it unchanged. Same functionality as the stock page (per-culture, grouped, permission-aware:
`ManageTranslations` or the per-culture permission to edit, `ViewDynamicTranslations` to view),
with two deliberate differences backed by `api/crest/translations`: reads include **orphaned**
entries (stored translations no provider currently enumerates — a disabled feature's strings,
an old key after a source string changed — flagged "stored only", editable and deletable), and
the save **merges**: only the rows the page displayed are replaced (blank deletes), everything
else in the store is carried over untouched. Nothing the page never showed can be destroyed by
saving.

**Closed upstream trap: the Translations editor's Save was lossy for deep captions.** Orchard's
`Save` replaces a culture's whole translation list with what the editor enumerated, and the
stock admin node localization providers enumerate *top-level* nodes only — so saving from the
Translations editor silently deleted stored translations for child-node captions (including
seeded ones). Crest registers `CrestAdminMenuChildCaptionDataLocalizationProvider`, which
enumerates every admin menu's below-root captions (roots stay upstream's, avoiding duplicate
rows) — making child captions visible and editable in the editor, and keeping their stored
values in the list Save round-trips instead of dropping them. The underlying upstream gaps are
logged in fruitful's `plans/upstream-orchard-proposals.md` (#2 non-recursive enumeration,
#3 wholesale save). Should a translation still go missing, `sync-providers` refills any seeded
entry on demand.

**Timing.** The import runs once per shell, on the first request that reads the admin menu. It
cannot run earlier: `INavigationManager.BuildMenuAsync` resolves each item's `Href` through
`IUrlHelper` and so needs an `ActionContext`, which does not exist during tenant activation.

**Disabled features.** A disabled feature's items cannot be imported ahead of time — its services
are never registered in the shell container (`CompositionStrategy` composes the container from
the shell descriptor's *enabled* features), so its provider cannot be constructed, and several
build their items from live tenant data. Instead, enabling or disabling a feature releases the
shell, so the next shell re-imports and picks up exactly what changed. Items that disappear are
marked disabled rather than deleted, so re-enabling the feature restores the tenant's icons,
ordering and renames against the same `UniqueId` instead of orphaning them.

`POST /api/crest/admin-menus/sync-providers` re-runs the import on demand and reports what
changed. It is idempotent.

## Item identity

Layout overrides are stored against each item's key, never against its caption — a caption is
translated, so keying on it would orphan every override as soon as the admin's culture changed.
Two mechanisms supply that key, and `NavigationItem.Key` prefers the first:

- `Id` — for items whose provider set one. Every stock admin node builder now copies
  `AdminNode.UniqueId` (a GUID assigned when the node is created, unaffected by later edits)
  onto `MenuItem.Id`, so DB-backed Admin Menu nodes have a stable identity that survives a
  caption being rewritten.
- `TextKey` — `MenuItem.Text.Name`, the invariant literal a provider passed to `S["..."]` —
  the localization key itself, English only by convention. This is what Orchard's own
  `NavigationManager.Merge` matches on, so it does not vary by culture. It is the fallback for
  items contributed by providers that set no `Id`.

## Renames are per culture

A rename is a translation of one caption, not a change of identity, so it is recorded against
the culture the admin was viewing when they typed it (`DisplayTextByCulture`). Renaming an item
under `es-ES` leaves every other culture's caption alone (each keeps its own translation, or
the invariant literal when none exists), and resolution falls back through the parent culture
(`es-ES`, then `es`) before the provider's own caption is used.

A rename recorded this way applies only inside Crest's sidebar. The **Save as this tenant's
translation** action next to a renamed item promotes it into the tenant's translation store —
the store `IDataLocalizer` reads at render time — so Orchard's own Razor admin renders the same
caption instead of disagreeing with Crest. Because that changes what every user of the tenant
sees in that culture, promotion additionally requires `ManageTranslations` (Administrator by
default) on top of the admin menu permission. The translation is keyed on the item's original
caption, scoped to the admin menu the node belongs to — the same context
(`DataLocalizationContext.AdminMenu(item.MenuName)`) that Orchard's own admin looks up.
Promoting after clearing a rename removes the translation again.

Promotion applies to any item with an owning admin menu — which, since the provider-menu import
above, includes former provider items: they are promoted against their imported node in
"Primary Navigation". The only item without an owning menu is the provider-owned "New" branch,
which promotion refuses; renaming it still works and still applies inside Crest's sidebar.
