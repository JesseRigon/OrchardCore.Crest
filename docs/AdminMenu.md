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

Items are matched across runs on `MenuItem.Text.Name` — the untranslated `S["..."]` literal that
OrchardCore's own `NavigationManager.Merge` matches on, so it does not vary by culture —
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
from a rename) always wins — and it re-runs on every sync, so adding a supported culture seeds
that culture on the next shell start (or `sync-providers` call). PO entries are keyed by
contributing class, which merging erases, so the seed matches the caption across the whole
catalog and takes the most common translation when contexts disagree. The result: every
imported caption is a first-class, per-tenant-editable entry in Orchard's own translations
editor (Configuration → Localization → Translations, under the "Primary Navigation" group),
pre-filled with what PO would have shown. One consequence to know: because seeded values are
tenant data from the moment they are written, a later PO catalog update only reaches
cultures/captions that were never seeded.

Crest's sidebar and app manifest resolve these captions through `IDataLocalizer` per request
culture at serialization time — the same resolution TheAdmin's `NavigationItemText.cshtml`
performs at render time — so both admins agree on what a caption looks like in any culture.
Items with no owning menu resolve under the generic "Admin Menus" context, as upstream does.

**Additive over upstream: the "New" branch reads "Content Types" translations.** Upstream
renders the New branch's content type captions under the generic "Admin Menus" context, which
no provider populates with type names — so a content type translated in the Translations
editor ("Content Types" group) shows translated on every content-editing surface yet
untranslated in the very menu that creates it. Crest closes that seam: an ownerless caption
with no "Admin Menus" translation falls back to the same caption's "Content Types"
translation, so one translation covers every surface a type name appears on.

**Known upstream trap: the Translations editor's Save is lossy for deep captions.** Orchard's
`Save` replaces a culture's whole translation list with what the editor enumerated, and the
stock admin node localization providers enumerate *top-level* nodes only — so saving from the
Translations editor silently deletes stored translations for child-node captions (including
seeded ones). The sync's seeding re-creates any that go missing on the next shell start or
`sync-providers` call, but a tenant's *hand-edited* child-caption translation does not come
back by itself. Fixing this needs the enumeration made recursive (upstream PR material).

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
- `TextKey` — `MenuItem.Text.Name`, the untranslated literal a provider passed to `S["..."]`.
  This is what Orchard's own `NavigationManager.Merge` matches on, so it does not vary by
  culture. It is the fallback for items contributed by providers that set no `Id`.

## Renames are per culture

A rename is a translation of one caption, not a change of identity, so it is recorded against
the culture the admin was viewing when they typed it (`DisplayTextByCulture`). Renaming an item
under `es-ES` leaves its English caption alone, and resolution falls back through the parent
culture (`es-ES`, then `es`) before the provider's own caption is used.

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
