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
    Tags = new[] { ManifestConstants.AdminTag, "crest", "legacy-frame", "hidden" }
)]
