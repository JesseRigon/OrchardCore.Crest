using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrchardCore.Environment.Shell;
using OrchardCore.Entities;
using OrchardCore.Localization;
using OrchardCore.Localization.Models;
using OrchardCore.Localization.Services;
using OrchardCore.Settings;

namespace Crest.Controllers;

[ApiController, AutoValidateAntiforgeryToken, Route("api/crest/localization")]
public sealed class CrestLocalizationController(
    ISiteService sites,
    IShellReleaseManager releases,
    IAuthorizationService authorization,
    ILocalizationService localizationService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<CrestLocalization>> GetAsync()
    {
        if (!await authorization.AuthorizeAsync(User, LocalizationPermissions.ManageCultures))
        {
            return Forbid();
        }

        return Ok(await CreateDtoAsync());
    }

    [HttpPut]
    public async Task<ActionResult<CrestLocalization>> SaveAsync(CrestLocalization request)
    {
        if (!await authorization.AuthorizeAsync(User, LocalizationPermissions.ManageCultures))
        {
            return Forbid();
        }

        string[] cultures;
        try
        {
            cultures = NormalizeCultures(request.SupportedCultures);
        }
        catch (CultureNotFoundException)
        {
            return BadRequest("Each supported culture must be a valid .NET/BCP 47 culture name.");
        }

        if (cultures.Length == 0)
        {
            return BadRequest("At least one supported culture is required.");
        }

        var defaultCulture = NormalizeCultureName(request.DefaultCulture);
        if (!cultures.Contains(defaultCulture, StringComparer.OrdinalIgnoreCase))
        {
            defaultCulture = cultures[0];
        }

        var site = await sites.LoadSiteSettingsAsync();
        site.Alter<LocalizationSettings>(settings =>
        {
            settings.SupportedCultures = cultures;
            settings.DefaultCulture = defaultCulture;
            settings.FallBackToParentCulture = request.FallBackToParentCulture;
        });
        await sites.UpdateSiteSettingsAsync(site);

        // This is Orchard's required lifecycle step: RequestLocalizationOptions are
        // rebuilt from the tenant's LocalizationSettings after the tenant reloads.
        releases.RequestRelease();

        return Ok(await CreateDtoAsync(cultures, defaultCulture, request.FallBackToParentCulture));
    }

    private async Task<CrestLocalization> CreateDtoAsync(
        string[]? cultures = null,
        string? defaultCulture = null,
        bool? fallBackToParentCulture = null)
    {
        var settings = await sites.GetSettingsAsync<LocalizationSettings>();
        cultures ??= await localizationService.GetSupportedCulturesAsync();
        defaultCulture ??= await localizationService.GetDefaultCultureAsync();
        fallBackToParentCulture ??= settings.FallBackToParentCulture;

        return new CrestLocalization(
            defaultCulture,
            cultures,
            fallBackToParentCulture.Value,
            localizationService.GetAllCulturesAndAliases()
                .GroupBy(culture => culture.Name, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .OrderBy(culture => culture.DisplayName, StringComparer.OrdinalIgnoreCase)
                .Select(CrestCulture.From)
                .ToArray());
    }

    private static string[] NormalizeCultures(IEnumerable<string>? values) => (values ?? [])
        .Where(value => value is not null)
        .Select(NormalizeCultureName)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

    private static string NormalizeCultureName(string? value)
    {
        var name = value?.Trim() ?? string.Empty;
        return string.IsNullOrEmpty(name) ? string.Empty : CultureInfo.GetCultureInfo(name).Name;
    }
}

public sealed record CrestLocalization(
    string DefaultCulture,
    string[] SupportedCultures,
    bool FallBackToParentCulture,
    CrestCulture[] AvailableCultures);

public sealed record CrestCulture(string Value, string Label, string NativeLabel)
{
    public static CrestCulture From(CultureInfo culture) => new(
        culture.Name,
        string.IsNullOrWhiteSpace(culture.DisplayName) ? culture.Name : culture.DisplayName,
        string.IsNullOrWhiteSpace(culture.NativeName) ? culture.DisplayName : culture.NativeName);
}
