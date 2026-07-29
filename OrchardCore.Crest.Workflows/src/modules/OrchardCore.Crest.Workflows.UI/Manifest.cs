using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Crest Workflows UI",
    Author = ManifestConstants.OrchardCoreTeam,
    Website = ManifestConstants.OrchardCoreWebsite,
    Version = ManifestConstants.OrchardCoreVersion
)]

[assembly: Feature(
    Id = "OrchardCore.Crest.Workflows.UI",
    Name = "UI Activities",
    Description = "Provides UI related activities.",
    Category = "Crest Workflows",
    Dependencies = ["OrchardCore.Crest.Workflows"]
)]