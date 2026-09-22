using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Crest Regions",
    Author = "OrchardCore.Crest",
    Version = "1.0.0",
    Category = "OrchardCore.Crest"
)]

[assembly: Feature(
    Id = "Crest.Regions",
    Name = "Crest Regions and Locations",
    Description = "Business Contexts (a party's geo/localization profile), the geo tree every place resolves into, and per-country addressing maps.",
    Category = "OrchardCore.Crest",
    Dependencies = ["OrchardCore.Crest", "OrchardCore.Contents", "OrchardCore.ContentFields", "OrchardCore.Title", "Crest.Global"]
)]
