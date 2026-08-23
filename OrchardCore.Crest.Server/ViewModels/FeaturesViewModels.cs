using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using OrchardCore.Environment.Extensions.Features;
using OrchardCore.Environment.Shell;
using OrchardCore.Environment.Shell.Descriptor;

namespace Crest.ViewModels;

public sealed record Feature(
    string Id,
    string Name,
    string Category,
    string Description,
    string ExtensionId,
    string[] Dependencies,
    bool AlwaysEnabled,
    bool Enabled,
    bool EnabledByDependencyOnly)
{
    public static Feature From(IFeatureInfo feature, bool enabled) => new(
        feature.Id,
        feature.Name ?? feature.Id,
        feature.Category ?? string.Empty,
        feature.Description ?? string.Empty,
        feature.Extension?.Id ?? string.Empty,
        feature.Dependencies ?? [],
        feature.IsAlwaysEnabled,
        enabled,
        feature.EnabledByDependencyOnly);

    public static Feature From(string id, IFeatureInfo? feature, bool enabled = true) => new(
        id,
        feature?.Name ?? id,
        feature?.Category ?? string.Empty,
        feature?.Description ?? string.Empty,
        feature?.Extension?.Id ?? string.Empty,
        feature?.Dependencies ?? [],
        feature?.IsAlwaysEnabled ?? false,
        enabled,
        feature?.EnabledByDependencyOnly ?? false);
}
