using OrchardCore.DisplayManagement.Manifest;
using OrchardCore.Modules.Manifest;

[assembly: Theme(
    Id = "OrchardCore.Crest.LegacyFrame",
    Name = "Orchard Crest UI Framework Legacy Frame",
    BaseTheme = "TheAdmin",
    Author = "Orchard Crest UI Framework",
    Website = "https://github.com/OrchardCore.Crest/Orchard-Crest",
    Version = "3.0.0.0.0",
    Description = "A stripped admin theme for rendering standard Orchard admin pages inside Orchard Crest UI Framework iframes.",
    Tags = new[] { ManifestConstants.AdminTag, "crest", "legacy-frame", "hidden" },
    // Always enabled rather than pulled in via OrchardCore.Crest's Dependencies: a module
    // can never hard-depend on a theme without a load-order cycle, because
    // ThemeExtensionDependencyStrategy gives every theme an implicit dependency on every
    // non-theme feature (so OrchardCore.Crest -> LegacyFrame -> OrchardCore.Crest). This
    // theme is Crest-owned infrastructure (LegacyFrameThemeSelector switches to it by Id
    // at runtime), never meant to be independently toggled, so always-enabled is correct
    // regardless of the cycle.
    IsAlwaysEnabled = true
)]
