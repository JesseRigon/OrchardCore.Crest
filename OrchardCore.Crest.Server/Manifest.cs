using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Crest Server",
    Author = "OrchardCore.Crest",
    Website = "https://crest.local",
    Version = "3.0.0.0.0",
    Description = "Provides Crest tenant APIs and server-side Orchard integrations.",
    Category = "OrchardCore.Crest"
)]

[assembly: Feature(
    Id = "OrchardCore.Crest",
    Name = "Crest Server",
    Description = "Provides Crest tenant APIs for authentication, theme settings, and app configuration.",
    Category = "OrchardCore.Crest",
    Dependencies = ["OrchardCore.Menu", "OrchardCore.Settings", "OrchardCore.Themes", "OrchardCore.Users", "OrchardCore.Crest.LegacyFrame", "OrchardCore.Crest.Icons"],
    IsAlwaysEnabled = true
)]

[assembly: Feature(
    Id = "OrchardCore.Crest.Icons",
    Name = "Orchard Crest UI Framework Icons",
    Description = "Provides packaged icon registry, local SVG icon sources, icon search, and icon pack delivery.",
    Category = "OrchardCore.Crest",
    IsAlwaysEnabled = true
)]

[assembly: Feature(
    Id = "OrchardCore.Crest.Icons.TenantMedia",
    Name = "Orchard Crest UI Framework Tenant Media Icons",
    Description = "Allows tenants to upload, index, search, and use their own SVG icons from Orchard Media storage.",
    Category = "OrchardCore.Crest",
    Dependencies = ["OrchardCore.Crest.Icons", "OrchardCore.Media"]
)]

[assembly: Feature(
    Id = "OrchardCore.Crest.DesignSystem",
    Name = "Crest Design System",
    Description = "Adds tenant-level design token editing for Orchard Crest UI Framework without switching Orchard themes.",
    Category = "OrchardCore.Crest",
    Dependencies = ["OrchardCore.Settings", "OrchardCore.Themes"]
)]
