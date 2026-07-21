using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using OrchardCore.Environment.Extensions;
using OrchardCore.Environment.Extensions.Features;
using OrchardCore.Environment.Shell;
using OrchardCore.Environment.Shell.Descriptor;

namespace Crest.Controllers;

[ApiController]
[IgnoreAntiforgeryToken]
[Route("api/crest/features")]
public sealed class FeaturesController(
    IShellDescriptorManager shellDescriptorManager,
    IExtensionManager extensionManager,
    IShellFeaturesManager shellFeaturesManager,
    IAuthorizationService authorizationService) : ControllerBase
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

    [HttpPost("{id}/enable")]
    public async Task<IActionResult> Enable(string id)
    {
        if (!await authorizationService.AuthorizeAsync(User, OrchardCore.Features.FeaturesPermissions.ManageFeatures))
        {
            return Forbid();
        }

        var feature = await FindFeatureAsync(id);
        if (feature is null)
        {
            return NotFound();
        }

        await shellFeaturesManager.EnableFeaturesAsync([feature], force: true);
        return NoContent();
    }

    [HttpPost("{id}/disable")]
    public async Task<IActionResult> Disable(string id)
    {
        if (!await authorizationService.AuthorizeAsync(User, OrchardCore.Features.FeaturesPermissions.ManageFeatures))
        {
            return Forbid();
        }

        var feature = await FindFeatureAsync(id);
        if (feature is null || feature.IsAlwaysEnabled)
        {
            return NotFound();
        }

        await shellFeaturesManager.DisableFeaturesAsync([feature], force: true);
        return NoContent();
    }

    private async Task<IFeatureInfo?> FindFeatureAsync(string id) => (await shellFeaturesManager.GetAvailableFeaturesAsync())
        .FirstOrDefault(feature => string.Equals(feature.Id, id, StringComparison.OrdinalIgnoreCase));
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
