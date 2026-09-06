# Content system — items, types, parts, fields, lists

How Crest surfaces Orchard's content system: the definition editors, the content
items surface, per-type menu presence, and the Content Part Lists enum system with
its option-picker machinery. This is an explanatory document — what exists, how it
works, and what will bite you — not an implementation plan. Open work lives in
[plans/content-items.md](../plans/content-items.md).

## Architecture: Orchard owns the model, Crest adapts it

Everything is a content item. Content TYPES are runtime-editable definitions
(parts + fields) stored per tenant; module migrations declare types via
`IContentDefinitionManager`, and tenants extend them through the type editor
without code. Code works against stable PARTS (typed accessors), never against
runtime-added fields — that is what makes runtime extension safe.

Crest adds no parallel content framework. Its server is a set of thin JSON
adapters (`api/crest/content-items`, `api/crest/content-types`,
`api/crest/content-part-lists`, `api/crest/option-sources`, …) over Orchard's own
services, and every adapter authorizes the real Orchard request principal with the
native Orchard permission before touching a service (`ICrestRequestAccess`).
Tenant scope, feature gates, validation and lifecycle are Orchard's; Crest must
not bypass, broaden, or persist parallel state.

**Contained items** are the recurring storage pattern: when child records have no
independent lifecycle, they live INSIDE the parent's document (one read, atomic
writes, frozen with the parent). Content Part Lists store their options this way
(`Options` on the list part), and consumers use Orchard's BagPart the same way for
things like transaction lines. The trade-off is deliberate: contained data is
document-only (no free SQL columns), so anything that must be queried across
documents needs an explicit YesSql map index.

## Definition editing: two screens, split by ownership

- The **Content Parts** page owns field DEFINITIONS — field type (conversion
  between types preserves every custom settings section; only the rebuilt
  `ContentPartFieldSettings` is replaced), add-field, the full option-picker
  attachment editor, and per-field visibility conditions. It owns them because a
  part is SHARED: a field change lands on every type carrying that part.
- The **Content Types** page owns which types carry which parts, the ATTACHMENT's
  settings (display name, description, position), type-level settings (Creatable,
  Securable, …), and the per-type Content-menu toggle.

Backed by `api/crest/content-types/*` and `GET/PUT/DELETE
api/crest/option-picker/attachment` (all `EditContentTypes`-gated). Settings
writes are FULL-REPLACE, never JSON-merge — a merge would resurrect deleted rows.
There are deliberately NO per-user instance overrides: what the admin configures
is the behaviour for everyone.

> **Gotcha — read-after-write:** `AlterTypeDefinitionAsync` /
> `AlterPartDefinitionAsync` changes are NOT visible to reads in the same request
> until the session commits. Anything that must react to a definition change
> (e.g. the menu sync) must run as a DEFERRED shell-scope task
> (`ShellScope.AddDeferredTask`), never inline — inline code reads the
> pre-change definitions.

## Content items surface

`GET api/crest/content-items` lists; the stock Orchard Contents REST endpoints
handle item CRUD (Crest does not duplicate them). When the list is filtered to a
type (`?contentType=X`), authorization runs `ListContent` against a stand-in item
of that type, so Securable types enforce the dynamic `ListContent_{Type}`
permission through Orchard's own `ContentTypeAuthorizationHandler`; the
unfiltered list checks global `ListContent`.

The admin ContentItems page accepts a `?type=` query parameter
(`SupplyParameterFromQuery`) that pre-applies the filter and swaps the heading to
the type's display name. It is a QUERY parameter, not a route segment, on
purpose: Crest's route-permission matching is path-segment-only
(query-invisible), so the existing `/Contents/ContentItems` registration and its
gate cover the filtered view with no new route template.

**Field visibility conditions** apply to ANY field type:
`CrestFieldVisibilitySettings { Path, Operator: Any|Equals|In, Keys }` on the
field definition. The editor shows/hides live and exempts hidden fields from
required-validation; the server CLEARS a hidden field's value on save
(`CrestFieldVisibilityEnforcer`, a fixpoint over chained conditions) — hidden
values are never kept dormant. Configured per field via `GET/PUT
api/crest/field-visibility` (wholesale replace; blank Path clears) and the
Visibility editor on the Content Parts screen. Keys follow the identity rule
below: picker parents resolve stored ids to KEYS before comparison, on both
sides.

## Per-type Content menu presence

A tenant-created type (a Blog, an FAQ) can appear in the admin Content menu
without any module declaring a page for it. `CrestContentTypeMenuSettings
{ ShowInContentMenu }` is a type-level settings POCO stored on the definition via
`MergeSettings<T>` — the same open settings bag every stock setting uses, so it
rides the ContentDefinition recipe step/deployment for free. Toggled on the
Content Types screen via `PUT api/crest/content-types/{type}/menu`
(`EditContentTypes`).

`ContentTypesMenuNavigationProvider` emits one entry per flagged type under the
stock "Content" section: caption = the type's DisplayName, URL = the generic
content-items page filtered by `?type=`, permission = the dynamic
`ListContent_{Type}` when the type is Securable, plain `ListContent` otherwise
(the dynamic name is only REGISTERED for securable types; an unregistered
permission name is silently dropped by role resolution and would leave the item
visible to everyone).

> **Gotcha — Crest's menu is materialized.** Stock Orchard rebuilds admin nav per
> request; Crest SYNCS provider output into the admin-menu document once per
> shell. The menu endpoint therefore triggers the provider sync itself — as a
> deferred task (see read-after-write above). Definition changes made elsewhere
> (a migration setting the flag) surface on the next shell release, which is when
> migrations run anyway.

> **Gotcha — renaming a type re-keys its menu entry.** The sync's match key is
> parent path + invariant caption, and the caption is the DisplayName. Renaming
> the display name disables the old node and creates a fresh one, so layout
> overrides stored against the old node no longer apply — re-place and re-export
> the layout. Two types sharing a DisplayName collide on the match key: don't do
> that. (Stock's `ContentTypesAdminNode` snapshots display names too; same
> property.)

Every flagged type lands on the generic content-items surface today; designed
per-type pages can replace that per type later without touching the menu
mechanism. Module-declared pages (e.g. Parties › Customers) are unaffected — this
is the additive path for types no module owns.

## Content Part Lists — the tenant-editable enum system

Orchard has no central enum store. `TextFieldPredefinedListEditorSettings` stores
options inside ONE field's settings (copies drift, no provenance, no multi-column
display); Taxonomies have the right data shape but are a CMS categorization
feature whose routing apparatus (AliasPart/AutoroutePart, term pages) is noise
for configuration data. So Crest defines a fully parallel, additive system —
`Crest.ContentPartLists`, its own feature, no dependency on
`OrchardCore.Taxonomies`, and **no upstream OrchardCore changes, ever**.

- **`ContentPartList`**: `TitlePart` + `CrestContentPartListPart` (`Key` — the
  set's stable logical key like `pricing.modifier-kind`; `Source`;
  `OptionContentType`; `DataLock`/`EditLock`; contained `Options`).
- **`Option`**: `TitlePart` + `CrestOptionPart` (`Key` — what CODE switches on;
  `Source` = Module|Tenant; `Hidden`; `Position`; `Category`;
  `DisplayTextPlural`; `Value`; a back-reference to its list).
- **`OptionPickerField`** is the reference mechanism, modelled file-for-file on
  stock `UserPickerField` (field + settings + drivers + handler + index provider).
  Stock `UserPickerField` stays in use for plain user references.

**The identity rule (load-bearing — everything else depends on it):** content
stores `ContentItemId`, code compares `Key`, humans read `DisplayText`, `Source`
says who put it there. `ContentItemId` is GENERATED per tenant — the same seeded
option has a different id in every tenant, so it can never be what code switches
on. `DisplayText` is freely renamable without breaking logic.

**The option surface beyond key + label:** `Category` (non-null, flat, one per
option — the dimension logic evaluates against, which is why the data lock
freezes it); `DisplayTextPlural` (null = fall back to singular; blank on update
CLEARS); `Value` (an optional second machine datum — a dial code on `US`, a
regex on a postal format; null for pure enums whose key IS their value);
`Position` (the list's only intrinsic order — everything else about ordering is
a per-attachment parameter).

**Locks — one mechanism, two authorities**, each valued None|Tenant|Module:
`DataLock` freezes the MACHINE surface (categories, the category vocabulary,
`Value`, data-locked custom fields) while labels, visibility and order stay
editable and tenant adds are allowed within existing categories; `EditLock` makes
the list read-only outright. Module locks are seed-declared, re-asserted on
reseed, and unliftable in-tenant; tenant locks need the security-critical
`LockContentPartLists` permission (deliberately NOT implied by
`ManageContentPartLists`; Administrator-only in practice).

> **Gotcha — lock enforcement lives at the CONTROLLER (409), not the service.**
> The service trusts module code, because migrations legitimately edit what
> tenants must not. Any new endpoint over the service must add its own lock
> check.

**Custom fields on option types.** Tenants add fields to option parts at runtime;
each list stores an `OptionContentType` (types carrying `CrestOptionPart` are
eligible; assignable from the list header; retargeting a POPULATED list is
refused because existing option items keep their stored type and would orphan).
Values travel stringified in a `Fields` bag (null = untouched, blank = clears,
unknown names 400); `CrestOptionFieldAccessor` is the ONE place that knows the
document JSON, and Numeric/Boolean fields store typed values — unparseable input
is refused, not stringified. Each field carries a per-field lock designation
(`CrestOptionFieldSettings.DataLocked`): machine surface (frozen with the data
lock) or display surface (default). The grid renders Boolean fields as switches
and Numeric fields as numeric inputs; the wire format stays strings and the
server owns typing.

**Global lists** are seeded by `Crest.ContentPartLists` for any enabling tenant:
country codes, UoM (with a separate `global.uom-rec20` mapping list — key = unit
key, Value = UN/CEFACT Rec 20 code; package units are deliberately absent, Rec 21
territory), phone country codes, postal-format regexes, languages, US area codes,
subdivisions, honorifics/suffixes. All module-data-locked except the
personal-name pair. What is deliberately NOT a list: currencies, time zones and
cultures are option-source PROVIDERS over the services that already own them
(Accounting's `ICurrencyProvider`, `TimeZoneInfo`, `CultureInfo`) — never a
parallel seeded copy.

> **Gotcha — seeding is DEFERRED.** List seeding runs as a deferred shell-scope
> task, never inline in a data migration: content creation during first-time
> tenant setup fires other features' handlers against index tables that do not
> exist yet. Consequently a consumer migration that attaches a picker to a type
> must ALSO defer its attach — an inline attach at first-time setup runs before
> the seeds exist and aborts provisioning.

**Deletion is asymmetric by design.** Tenant-owned lists delete
(options are contained, nothing orphans); module-seeded lists refuse deletion —
their keys are the module's contract — and can only be hidden. Individual
options only hide, for history.

> **Gotcha — don't convert a big list to a content type "for indexing".**
> Option data would still be document-only, the conversion costs single-document
> atomicity, the lock model's home, and cheap reorder/reseed. If ONE list
> outgrows enum scale (thousands of options), promote THAT list alone: new
> content type + flip the attachment's `SourceKey` to `contentitem:<Type>` — the
> provider seam makes it a per-attachment swap.

## Option pickers: projection, providers, filtering

A dropdown stores ONE id but displays N columns of the referenced record. The
columns are per-ATTACHMENT settings (the same target renders differently in
different dropdowns), never part of the stored data:
`OptionPickerFieldSettings { SourceKey, Multiple, Columns, DisplayTemplate,
SearchColumns, SortColumns, Filters }`. Stored data stays tiny:
`{ SourceKey, SelectedIds }`.

**The provider seam** (`IOptionSourceProvider`: `Key`, `DescribeColumnsAsync`,
`QueryAsync`, batched `GetByIdsAsync`) keeps the field ignorant of what it lists.
Registration is DI-only. Built-in providers: `contentpartlist` (tenant
configuration — always fully in memory, one document), `users` (multi-column,
incl. `Property:<dotted.path>` into user JSON), `contentitem` (entity records,
real SQL pushdown), `currency`, `timezone`, `culture`. Column `Path` is
provider-interpreted (each provider advertises its own paths); custom-field
columns follow the `Field:<Name>` convention. Contract for all providers:
`GetByIdsAsync` returns rows that are ABSENT when unresolvable, never hollow.

**Sort is an instance parameter, not list data.** Per-attachment or per-query
`SortColumns`, applied by every provider through the shared `OptionRowSorter`
("categorized" is just `["Category", "DisplayText"]`). The manual order
(`Position`) is the only order the list itself owns.

**Filtering — three axes, composed with AND:**

1. **Static filters** (`OptionFilter.Value`) — literals fixed at configuration
   time ("only active customers").
2. **Dependent filters** (`OptionFilter.ValueFrom`) — the value is read from
   another field on the item being edited, which is how cascading dropdowns fall
   out of the same mechanism (Country → Region). A picker parent's stored ids
   resolve through its OWN source and project to a configured column
   (`ValueFromColumn`; null/"Key" = the option's technical Key). Resolution
   fails CLOSED: an unresolvable parent yields empty, never "matches
   everything". `RequireParentValue` makes an empty parent mean an empty child;
   `OnParentChange` is `WarnThenClear|SilentClear`; re-query fires when the
   parent COMMITS, not per keystroke.
3. Caller-supplied narrowing is deferred (see the plan doc); if ever built it
   must be narrowing-only.

> **Warning — filters are not authorization.** A filter says what a dropdown
> DISPLAYS; scope says what a USER may see. The query endpoint accepts NO
> filters from the client — the picker sends `ContentType` + `FieldName` +
> `EditorState`, and the server loads that attachment's filters itself. A client
> that could post filters could query any source with any predicate.

**Data scope** builds on Orchard's own authorization (`ViewContent` /
`ViewOwnContent` via `AuthorizeContentTypeAsync`), bucketing any/own like
stock's admin list filter, fail-closed (`Where(x => false)` when nothing is
viewable; "may see nothing" is DISTINCT from "unrestricted"). Content part
lists are unrestricted: they are tenant configuration. **Assignment** is a
separate model from ownership (`Owner` = who created it; assignment = who
handles it): `CrestAssignmentPart` holds N assignments per item, indexed one row
per assignment, and scope-layering only ever NARROWS the permission result.
Unresolvable references make the REFERENCING item out of scope too (transitive,
depth-budgeted, budget exhaustion DENIES) — a hollow record is worse than an
absent one.

**Snapshot rule:** `OptionRow` is for editing and display only. When an option
lands on a posted document, the projected display text is FROZEN onto that
document; history never re-reads the live record.

## Indexing

`OptionPickerFieldIndex` is partitioned PER CONTENT TYPE (declaration-based —
a new partition is a migration, not code), one row per selected id, with
`SelectedKey` carried so code can query by key with no id→key join. Display
columns are never indexed — they are projections of the referenced record.

The `contentitem` provider pushes sort and filter into SQL where it can
(`PlanSqlSort`, `PlanFieldFilterPromotion` over the stock
Text/Numeric/BooleanFieldIndex tables — Crest's Server manifest depends on
`OrchardCore.ContentFields.Indexing.SQL` so those rows exist). Known,
ACCEPTED boundaries:

- Text filters deliberately never promote to SQL: the in-memory matcher is
  case-insensitive, SQL equality follows DB collation — semantics must not
  change with the execution path.
- Pushed sorts use SQL collation, not the request culture (matches every stock
  Orchard list).
- A field-index join excludes items last saved before the field existed (stock
  caveat — re-save or rebuild the index).
- One join per index type per query (YesSql merges same-type joins).

## Quick gotcha checklist

- Definition writes are invisible until commit → react in DEFERRED shell tasks.
- List seeding and picker attaches in migrations must both be deferred.
- Lock enforcement is at the controller; the service trusts callers.
- Blank-on-update CLEARS (plural, Value, custom fields); null means untouched.
- Renaming a type's DisplayName re-keys its synced menu entry.
- Unregistered permission names are silently dropped from menu items — only use
  dynamic `…_{Type}` names for Securable types.
- Settings writes are full-replace; JSON-merge resurrects deleted rows.
- The query endpoint never accepts client filters; filters ≠ authorization.
- Providers must return absent (not hollow) rows for unresolvable ids; parent
  resolution fails closed.
- `ContentItemId` differs per tenant; code compares `Key`, always.
- Unsafe (`POST`/`PUT`/`DELETE`) Crest endpoints require the Orchard antiforgery
  double-token; a missing header is a 400, not a 403.
