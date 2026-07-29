using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Crest Workflows Contents",
    Author = ManifestConstants.OrchardCoreTeam,
    Website = ManifestConstants.OrchardCoreWebsite,
    Version = ManifestConstants.OrchardCoreVersion
)]

[assembly: Feature(
    Id = "OrchardCore.Crest.Workflows.Contents",
    Name = "Content Activities",
    Description = "Provides content related activities.",
    Category = "Crest Workflows",
    Dependencies = ["OrchardCore.Crest.Workflows", "OrchardCore.Contents", "OrchardCore.Title", "OrchardCore.Taxonomies"]
)]