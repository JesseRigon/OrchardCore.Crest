# Crest.Admin.Client localization fixtures

Reference copies of the hand-written `msgctxt "Crest.Admin.Client"` `.po` entries that
seed the WASM `.Admin` client string catalog (`CrestApiLocalizer`, served via
`GET api/crest/localization/strings` — see `plans/user-localization.md`'s "Client string
localization for `.Admin`"). The files actually consumed at runtime live at
`OrchardCore.Crest.Server/Localization/{culture}.po` — `ModularPoFileLocationProvider`
resolves each extension's own `Localization/` folder using its `SubPath`, and the Crest
server module's extension id is `OrchardCore.Crest` (see `Manifest.cs`), so this is the
correct autoscan location — no global `/Localization/{culture}/*.po` entry is needed.

Seeded with `AdminMenus_Loading`/`AdminMenus_NoneFound` (the original `AdminMenus.razor`
proof-of-concept keys) plus a representative sample added 2026-08-07 spanning Login,
Profile, Menus, Indexes, Templates, and AdminStatus — enough to live-verify the full
`.po` → API → `CrestApiLocalizer` → rendered UI pipeline for phase 6's file-by-file
conversion (see `plans/user-localization.md` phase 6) without hand-translating every
converted key. Extend both this directory and the server's copy together if more keys
need test coverage.

Keep both copies in sync by hand; there's no build step that generates one from the
other.
