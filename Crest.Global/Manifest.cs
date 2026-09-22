using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Crest Global Reference Data",
    Author = "OrchardCore.Crest",
    Version = "1.0.0",
    Category = "OrchardCore.Crest"
)]

// The shell-side half: the permission that gates super-tenant editing and, later, the
// generated editing UI. The store itself is host-level and registered from Program.cs
// via AddCrestGlobalStore, not from this feature.
[assembly: Feature(
    Id = "Crest.Global",
    Name = "Crest Global Reference Data",
    Description = "Reference data shared by every tenant, editable only from the Default tenant.",
    Category = "OrchardCore.Crest",
    Dependencies = ["OrchardCore.Crest"]
)]
