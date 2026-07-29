using OrchardCore.Modules.Manifest;
using OrchardCore.OpenId;

[assembly: Module(
    Author = ManifestConstants.OrchardCoreTeam,
    Website = ManifestConstants.OrchardCoreWebsite,
    Version = ManifestConstants.OrchardCoreVersion,
    Name = "Crest Workflows"
)]

[assembly: Feature(
    Id = "OrchardCore.Crest.Workflows",
    Name = "Crest Workflows",
    Description = "Provides foundational Elsa Workflows services.",
    Category = "Crest Workflows",
    Dependencies = ["OrchardCore.Contents", OpenIdConstants.Features.Core]
)]

[assembly: Feature(
    Id = "OrchardCore.Crest.Workflows.Http",
    Name = "HTTP Activities",
    Description = "Provides HTTP activities.",
    Category = "Crest Workflows",
    Dependencies = ["OrchardCore.Crest.Workflows"]
)]