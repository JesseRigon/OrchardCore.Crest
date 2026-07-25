# OrchardCore.Crest.Iconify

`OrchardCore.Crest.Iconify` owns the Iconify-specific integration layer for Orchard Crest UI Framework.

It provides:

- C# provider settings for Iconify-compatible APIs;
- local full-library mirror contracts and implementation;
- build/runtime switches for using the bundled Iconify source cache.

It does not depend on `OrchardCore.Crest.Icons`. The dependency direction is `OrchardCore.Crest.Icons -> OrchardCore.Crest.Iconify`.

The local full-library cache setting is exposed in C# as `IconifyIconProviderSettings.LocalLibraryCacheEnabled` and defaults to `true`. UI for changing this setting is intentionally not exposed yet.

