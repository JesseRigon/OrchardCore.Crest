# OrchardCore.Crest.Icons

`OrchardCore.Crest.Icons` owns icon infrastructure for Orchard Crest UI Framework:

- icon provider contracts and settings;
- icon registry/search/pack services;
- public Iconify cache integration;
- tenant media icon support;
- icon UI such as `IconSelector`;
- icon-specific CSS, JavaScript, and static assets.

This module may depend on `OrchardCore.Crest.Components` for shared Radzen-backed UI primitives.

`OrchardCore.Crest.Components` must not depend on this module. Admin/Site themes reference both projects when they need icon UI compiled into the WASM app.

Icon-specific component CSS should live here, preferably under:

```text
wwwroot/icons.css
wwwroot/icons.js
```

Theme stylesheets should only contain intentional theme overrides.
