using OrchardCore.DisplayManagement.Manifest;
using OrchardCore.Modules.Manifest;

[assembly: Theme(
    Id = "BlazingOrchard.LegacyFrame",
    Name = "Blazing Orchard Legacy Frame",
    BaseTheme = "TheAdmin",
    Author = "Blazing Orchard",
    Website = "https://github.com/BlazingOrchard/Blazing-Orchard",
    Version = "3.0.0.0.0",
    Description = "A stripped admin theme for rendering standard Orchard admin pages inside Blazing Orchard iframes.",
    Tags = new[] { ManifestConstants.AdminTag, "blazing", "legacy-frame", "hidden" }
)]
