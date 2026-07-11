using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement.Extensions;
using OrchardCore.Environment.Extensions;
using OrchardCore.Environment.Extensions.Features;
using OrchardCore.Environment.Shell;
using OrchardCore.Modules.Manifest;
using OrchardCore.Themes;
using OrchardCore.Themes.Services;

namespace BlazingOrchard.Controllers;

[ApiController]
[IgnoreAntiforgeryToken]
[Route("api/blazing/themes")]
public sealed class ThemesController(
    ISiteThemeService siteThemeService,
    IAdminThemeService adminThemeService,
    IShellFeaturesManager shellFeaturesManager,
    IAuthorizationService authorizationService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ThemesState>> List()
    {
        if (!await authorizationService.AuthorizeAsync(User, OrchardCore.Themes.Permissions.ApplyTheme))
        {
            return Forbid();
        }

        var currentSiteTheme = await siteThemeService.GetSiteThemeAsync();
        var currentAdminTheme = await adminThemeService.GetAdminThemeAsync();
        var enabledFeatures = await shellFeaturesManager.GetEnabledFeaturesAsync();
        var enabledIds = enabledFeatures.Select(feature => feature.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var themes = (await shellFeaturesManager.GetAvailableFeaturesAsync())
            .Where(IsSelectableTheme)
            .Select(feature => ThemeSummary.From(
                feature,
                IsAdminTheme(feature.Extension.Manifest),
                enabledIds.Contains(feature.Id),
                string.Equals(feature.Id, IsAdminTheme(feature.Extension.Manifest) ? currentAdminTheme?.Id : currentSiteTheme?.Id, StringComparison.OrdinalIgnoreCase),
                GetThemeImageUrl(feature)))
            .OrderByDescending(theme => theme.IsCurrent)
            .ThenBy(theme => theme.IsAdmin)
            .ThenBy(theme => theme.Name)
            .ToArray();

        return Ok(new ThemesState(
            currentSiteTheme?.Id,
            currentAdminTheme?.Id,
            themes.FirstOrDefault(theme => string.Equals(theme.Id, currentSiteTheme?.Id, StringComparison.OrdinalIgnoreCase)),
            themes.FirstOrDefault(theme => string.Equals(theme.Id, currentAdminTheme?.Id, StringComparison.OrdinalIgnoreCase)),
            themes));
    }

    [HttpPost("{id}/current")]
    public async Task<IActionResult> SetCurrent(string id)
    {
        if (!await authorizationService.AuthorizeAsync(User, OrchardCore.Themes.Permissions.ApplyTheme))
        {
            return Forbid();
        }

        var feature = await FindThemeAsync(id);
        if (feature is null)
        {
            return NotFound();
        }

        if (IsAdminTheme(feature.Extension.Manifest))
        {
            await adminThemeService.SetAdminThemeAsync(feature.Id);
        }
        else
        {
            await siteThemeService.SetSiteThemeAsync(feature.Id);
        }

        var enabledFeatures = await shellFeaturesManager.GetEnabledFeaturesAsync();
        if (!enabledFeatures.Any(enabled => string.Equals(enabled.Id, feature.Id, StringComparison.OrdinalIgnoreCase)))
        {
            await shellFeaturesManager.EnableFeaturesAsync([feature], force: true);
        }

        return NoContent();
    }

    [HttpPost("reset-site")]
    public async Task<IActionResult> ResetSiteTheme()
    {
        if (!await authorizationService.AuthorizeAsync(User, OrchardCore.Themes.Permissions.ApplyTheme))
        {
            return Forbid();
        }

        await siteThemeService.SetSiteThemeAsync(string.Empty);
        return NoContent();
    }

    [HttpPost("reset-admin")]
    public async Task<IActionResult> ResetAdminTheme()
    {
        if (!await authorizationService.AuthorizeAsync(User, OrchardCore.Themes.Permissions.ApplyTheme))
        {
            return Forbid();
        }

        await adminThemeService.SetAdminThemeAsync(string.Empty);
        return NoContent();
    }

    [HttpPost("{id}/enable")]
    public async Task<IActionResult> Enable(string id)
    {
        if (!await authorizationService.AuthorizeAsync(User, OrchardCore.Themes.Permissions.ApplyTheme))
        {
            return Forbid();
        }

        var feature = await FindThemeAsync(id);
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
        if (!await authorizationService.AuthorizeAsync(User, OrchardCore.Themes.Permissions.ApplyTheme))
        {
            return Forbid();
        }

        var feature = await FindThemeAsync(id);
        if (feature is null)
        {
            return NotFound();
        }

        await shellFeaturesManager.DisableFeaturesAsync([feature], force: true);
        return NoContent();
    }

    private async Task<IFeatureInfo?> FindThemeAsync(string id) => (await shellFeaturesManager.GetAvailableFeaturesAsync())
        .FirstOrDefault(feature => string.Equals(feature.Id, id, StringComparison.OrdinalIgnoreCase) && IsSelectableTheme(feature));

    private static bool IsSelectableTheme(IFeatureInfo feature)
    {
        if (feature.IsAlwaysEnabled || feature.EnabledByDependencyOnly || !feature.IsTheme())
        {
            return false;
        }

        return !feature.Extension.Manifest.Tags.Any(tag => string.Equals(tag, "hidden", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsAdminTheme(IManifestInfo manifest) => manifest.Tags
        .Any(tag => string.Equals(tag, ManifestConstants.AdminTag, StringComparison.OrdinalIgnoreCase));

    private static string GetThemeImageUrl(IFeatureInfo feature) => $"/{feature.Extension.Id}/Theme.png";
}

public sealed record ThemesState(
    string? CurrentSiteThemeId,
    string? CurrentAdminThemeId,
    ThemeSummary? CurrentSiteTheme,
    ThemeSummary? CurrentAdminTheme,
    ThemeSummary[] Themes);

public sealed record ThemeSummary(
    string Id,
    string Name,
    string Description,
    string Author,
    string Website,
    string Version,
    string ExtensionId,
    bool IsAdmin,
    bool IsCurrent,
    bool Enabled,
    string PreviewImageUrl)
{
    public static ThemeSummary From(IFeatureInfo feature, bool isAdmin, bool enabled, bool isCurrent, string previewImageUrl) => new(
        feature.Id,
        feature.Name ?? feature.Id,
        feature.Description ?? string.Empty,
        feature.Extension.Manifest.Author ?? string.Empty,
        feature.Extension.Manifest.Website ?? string.Empty,
        feature.Extension.Manifest.Version ?? string.Empty,
        feature.Extension.Id,
        isAdmin,
        isCurrent,
        enabled,
        previewImageUrl);
}
