using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Blazing Server",
    Author = "Blazing",
    Website = "https://blazing.local",
    Version = "3.0.0.0.0",
    Description = "Provides Blazing tenant APIs and server-side Orchard integrations.",
    Category = "Blazing"
)]

[assembly: Feature(
    Id = "Blazing",
    Name = "Blazing Server",
    Description = "Provides Blazing tenant APIs for authentication, theme settings, and app configuration.",
    Category = "Blazing",
    Dependencies = ["OrchardCore.Menu", "OrchardCore.Settings", "OrchardCore.Themes", "OrchardCore.Users", "BlazingOrchard.LegacyFrame", "BlazingOrchard.Icons"],
    IsAlwaysEnabled = true
)]

[assembly: Feature(
    Id = "BlazingOrchard.Icons",
    Name = "Blazing Orchard Icons",
    Description = "Provides packaged icon registry, local SVG icon sources, icon search, and icon pack delivery.",
    Category = "Blazing",
    IsAlwaysEnabled = true
)]

[assembly: Feature(
    Id = "BlazingOrchard.Icons.TenantMedia",
    Name = "Blazing Orchard Tenant Media Icons",
    Description = "Allows tenants to upload, index, search, and use their own SVG icons from Orchard Media storage.",
    Category = "Blazing",
    Dependencies = ["BlazingOrchard.Icons", "OrchardCore.Media"]
)]

[assembly: Feature(
    Id = "BlazingOrchard.DesignSystem",
    Name = "Blazing Design System",
    Description = "Adds tenant-level design token editing for Blazing Orchard without switching Orchard themes.",
    Category = "Blazing",
    Dependencies = ["OrchardCore.Settings", "OrchardCore.Themes"]
)]
