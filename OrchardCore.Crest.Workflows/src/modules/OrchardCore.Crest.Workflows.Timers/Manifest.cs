using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Crest Workflows Timers",
    Author = ManifestConstants.OrchardCoreTeam,
    Website = ManifestConstants.OrchardCoreWebsite,
    Version = ManifestConstants.OrchardCoreVersion
)]

[assembly: Feature(
    Id = "OrchardCore.Crest.Workflows.Timers",
    Name = "Timer Services and Activities",
    Description = "Provides common timer services and activities.",
    Category = "Crest Workflows",
    Dependencies = ["OrchardCore.Crest.Workflows"]
)]

[assembly: Feature(
    Id = "OrchardCore.Crest.Workflows.Timers.Quartz",
    Name = "Quartz Timer Provider",
    Description = "Provides Quartz-based timer services. Suitable for clustered deployments.",
    Category = "Crest Workflows",
    Dependencies = ["OrchardCore.Crest.Workflows.Timers"]
)]