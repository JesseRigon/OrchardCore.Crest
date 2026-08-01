# Crest.Admin.Client localization fixtures

Reference copies of the hand-written `msgctxt "Crest.Admin.Client"` `.po` entries that
seed the WASM `.Admin` client string catalog (`CrestApiLocalizer`, served via
`GET api/crest/localization/strings` — see `plans/user-localization.md`'s "Client string
localization for `.Admin`"). The files actually consumed at runtime live at
`OrchardCore.Crest.Server/Localization/{culture}.po` — `ModularPoFileLocationProvider`
resolves each extension's own `Localization/` folder using its `SubPath`, and the Crest
server module's extension id is `OrchardCore.Crest` (see `Manifest.cs`), so this is the
correct autoscan location — no global `/Localization/{culture}/*.po` entry is needed.

Two keys are seeded (`AdminMenus_Loading`, `AdminMenus_NoneFound`), matching real keys
already converted to the `T.T(key, fallback)` pattern in `AdminMenus.razor` (the
proof-of-concept file for that conversion — see `plans/user-localization.md` phase 5/6).
Extend both this directory and the theme's copy together if more keys need test coverage.

Keep both copies in sync by hand; there's no build step that generates one from the
other.
