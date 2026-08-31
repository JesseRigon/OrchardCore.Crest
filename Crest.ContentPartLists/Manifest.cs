using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Crest Content Part Lists",
    Author = "OrchardCore.Crest",
    Website = "https://crest.local",
    Version = "3.0.0.0.0",
    Description = "Tenant-editable content part lists (enums) with shared global sets.",
    Category = "OrchardCore.Crest"
)]

// Deliberately NOT IsAlwaysEnabled: a tenant opts into Content Part Lists. Consumers that
// require it (the Fruitful modules) declare it in their own manifest Dependencies so
// enabling them enables this - features, not recipes, carry prerequisites.
[assembly: Feature(
    Id = "Crest.ContentPartLists",
    Name = "Crest Content Part Lists",
    Description = "Named, tenant-editable sets of options that content types can reference. Ships shared global sets (country codes, units of measure) every enabling tenant receives.",
    Category = "OrchardCore.Crest",
    Dependencies = ["OrchardCore.Crest", "OrchardCore.Contents", "OrchardCore.ContentFields"]
)]
