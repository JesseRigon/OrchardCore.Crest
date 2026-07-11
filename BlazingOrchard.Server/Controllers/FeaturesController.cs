using Microsoft.AspNetCore.Mvc;
using OrchardCore.Environment.Extensions;
using OrchardCore.Environment.Shell.Descriptor;

namespace BlazingOrchard.Controllers;

[ApiController]
[IgnoreAntiforgeryToken]
[Route("api/blazing/features")]
public sealed class FeaturesController(
    IShellDescriptorManager shellDescriptorManager,
    IExtensionManager extensionManager) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<Feature[]>> List()
    {
        var descriptor = await shellDescriptorManager.GetShellDescriptorAsync();
        var enabledIds = descriptor.Features.Select(feature => feature.Id).ToArray();
        var featureInfos = extensionManager.GetFeatures(enabledIds.AsEnumerable()).ToDictionary(feature => feature.Id);

        return Ok(enabledIds
            .Select(id => Feature.From(id, featureInfos.GetValueOrDefault(id)))
            .OrderBy(feature => feature.Category)
            .ThenBy(feature => feature.Name)
            .ToArray());
    }
}

public sealed record Feature(
    string Id,
    string Name,
    string Category,
    string Description,
    string ExtensionId,
    string[] Dependencies,
    bool AlwaysEnabled)
{
    public static Feature From(string id, OrchardCore.Environment.Extensions.Features.IFeatureInfo? feature) => new(
        id,
        feature?.Name ?? id,
        feature?.Category ?? string.Empty,
        feature?.Description ?? string.Empty,
        feature?.Extension?.Id ?? string.Empty,
        feature?.Dependencies ?? [],
        feature?.IsAlwaysEnabled ?? false);
}
