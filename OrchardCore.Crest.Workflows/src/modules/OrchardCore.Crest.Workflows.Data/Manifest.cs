using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Crest Workflows Data",
    Author = ManifestConstants.OrchardCoreTeam,
    Website = ManifestConstants.OrchardCoreWebsite,
    Version = ManifestConstants.OrchardCoreVersion
)]

[assembly: Feature(
    Id = "OrchardCore.Crest.Workflows.Data.Csv",
    Name = "CSV Activities",
    Description = "Provides CSV related activities.",
    Category = "Crest Workflows",
    Dependencies = ["OrchardCore.Crest.Workflows"]
)]