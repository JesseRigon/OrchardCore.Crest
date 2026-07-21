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
    Dependencies = ["OrchardCore.Menu", "OrchardCore.Settings", "OrchardCore.Themes", "OrchardCore.Users", "BlazingOrchard.LegacyFrame"],
    IsAlwaysEnabled = true
)]

[assembly: Feature(
    Id = "BlazingOrchard.DesignSystem",
    Name = "Blazing Design System",
    Description = "Adds tenant-level design token editing for Blazing Orchard without switching Orchard themes.",
    Category = "Blazing",
    Dependencies = ["OrchardCore.Settings", "OrchardCore.Themes"]
)]
