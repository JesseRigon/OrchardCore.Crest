using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using OrchardCore.Environment.Extensions.Features;
using OrchardCore.Environment.Shell;
using OrchardCore.Environment.Shell.Descriptor;
using Crest.ViewModels;

namespace Crest.Controllers;

[ApiController]
[AutoValidateAntiforgeryToken]
[Route("api/crest/features")]
public sealed class FeaturesController(
    IShellDescriptorManager shellDescriptorManager,
    IShellFeaturesManager shellFeaturesManager,
    IAuthorizationService authorizationService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<Feature[]>> List()
    {
        if (!await authorizationService.AuthorizeAsync(User, OrchardCore.Features.FeaturesPermissions.ManageFeatures)) return Forbid();
        var descriptor = await shellDescriptorManager.GetShellDescriptorAsync();
        var enabledIds = descriptor.Features
            .Select(feature => feature.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var featureInfos = await shellFeaturesManager.GetAvailableFeaturesAsync();

        return Ok(featureInfos
            .Select(feature => Feature.From(feature, enabledIds.Contains(feature.Id)))
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
