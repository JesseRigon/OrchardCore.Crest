using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using OrchardCore.Environment.Shell;
using OrchardCore.Entities;
using OrchardCore.Localization;
using OrchardCore.Localization.Models;
using OrchardCore.Localization.Services;
using OrchardCore.Settings;
using OrchardCore.Users;
using OrchardCore.Users.Localization.Models;
using OrchardCore.Users.Models;

namespace Crest.Controllers;

[ApiController, AutoValidateAntiforgeryToken, Route("api/crest/localization")]
public sealed class CrestLocalizationController(
    ISiteService sites,
    IShellReleaseManager releases,
    IAuthorizationService authorization,
    ILocalizationService localizationService,
    UserManager<IUser> userManager,
    ILocalizationManager localizationManager) : ControllerBase
{
    // Crest.Admin (Blazor WASM) has no server-side round trip per render, so it cannot
    // use IStringLocalizer<T> directly the way server-rendered Razor/CRM.AdminMenu.cs
    // does. Baking .resx satellite assemblies into the WASM bundle (as
    // OrchardCore.Crest.Components already does for its own component-library strings)
    // was explicitly rejected for .Admin's own UI strings: those need to be editable the
    // same way any other translatable content is - via OrchardCore's normal localization
    // tooling (.po files) - without a recompile/redeploy. So instead: this endpoint
    // exposes the resolved .po catalog for a requested culture, scoped to a fixed
    // CrestClientStringsContext, and the client (see Crest.Admin.Theme.ApiLocalizer)
    // fetches and caches it, plugging into the same Localizer/ILocalizer seam
    // OrchardCore.Crest.Components already defines for exactly this kind of override.
    public const string ClientStringsContext = "Crest.Admin.Client";

    [HttpGet("strings")]
    public ActionResult<Dictionary<string, string>> GetStrings(string culture)
    {
        if (string.IsNullOrWhiteSpace(culture))
        {
            return BadRequest("Culture is required.");
        }

        CultureInfo cultureInfo;
        try
        {
            cultureInfo = CultureInfo.GetCultureInfo(culture);
        }
        catch (CultureNotFoundException)
        {
            return BadRequest("Culture must be a valid .NET/BCP 47 culture name.");
        }

        var dictionary = localizationManager.GetDictionary(cultureInfo);
        var strings = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (key, values) in dictionary.Translations)
        {
            if (string.Equals(key.Context, ClientStringsContext, StringComparison.Ordinal) && values.Length > 0)
            {
                strings[key.MessageId] = values[0];
            }
        }

        return Ok(strings);
    }

    // Self-service: the current user's own stored default culture
    // (OrchardCore.Users.Localization's UserLocalizationSettings, on User.Properties —
    // see plans/user-localization.md). Deliberately scoped to "current user only", not
    // gated by LocalizationPermissions.ManageCultures/user-management permissions the way
    // the stock admin "edit user" screen is, since every signed-in user manages their own
    // preference here. Shared by the culture dropdown's "Save as default" action and the
    // Settings page's localization section — one write path, not duplicated.
    [HttpGet("me")]
    public async Task<ActionResult<CrestUserCulture>> GetMyCultureAsync()
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return Forbid();
        }

        var user = await userManager.GetUserAsync(User) as User;
        if (user is null)
        {
            return NotFound();
        }

        user.TryGet<UserLocalizationSettings>(out var settings);
        return Ok(new CrestUserCulture(settings?.Culture));
    }

    [HttpPut("me")]
    public async Task<ActionResult<CrestUserCulture>> SetMyCultureAsync(CrestUserCulture request)
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return Forbid();
        }

        var user = await userManager.GetUserAsync(User) as User;
        if (user is null)
        {
            return NotFound();
        }

        string? culture = null;
        if (!string.IsNullOrWhiteSpace(request.Culture))
        {
            try
            {
                culture = CultureInfo.GetCultureInfo(request.Culture).Name;
            }
            catch (CultureNotFoundException)
            {
                return BadRequest("Culture must be a valid .NET/BCP 47 culture name.");
            }
        }

        user.Alter<UserLocalizationSettings>(settings => settings.Culture = culture);
        var result = await userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            return StatusCode(StatusCodes.Status500InternalServerError);
        }

        return Ok(new CrestUserCulture(culture));
    }

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

        // AdminDefaultCulture (rung 3 of the client resolution chain - see
        // plans/user-localization.md's "Resolution architecture") is optional and, when
        // set, must still be one of the tenant's supported cultures - it is not a way to
        // force an unsupported culture onto the admin area.
        string? adminDefaultCulture = null;
        if (!string.IsNullOrWhiteSpace(request.AdminDefaultCulture))
        {
            adminDefaultCulture = NormalizeCultureName(request.AdminDefaultCulture);
            if (!cultures.Contains(adminDefaultCulture, StringComparer.OrdinalIgnoreCase))
            {
                return BadRequest("Admin default culture must be one of the supported cultures.");
            }
        }

        var site = await sites.LoadSiteSettingsAsync();
        site.Alter<LocalizationSettings>(settings =>
        {
            settings.SupportedCultures = cultures;
            settings.DefaultCulture = defaultCulture;
            settings.FallBackToParentCulture = request.FallBackToParentCulture;
        });
        site.Alter<CrestLocalizationSettings>(settings => settings.AdminDefaultCulture = adminDefaultCulture);
        await sites.UpdateSiteSettingsAsync(site);

        // This is Orchard's required lifecycle step: RequestLocalizationOptions are
        // rebuilt from the tenant's LocalizationSettings after the tenant reloads.
        releases.RequestRelease();

        return Ok(await CreateDtoAsync(cultures, defaultCulture, request.FallBackToParentCulture, adminDefaultCulture));
    }

    private async Task<CrestLocalization> CreateDtoAsync(
        string[]? cultures = null,
        string? defaultCulture = null,
        bool? fallBackToParentCulture = null,
        string? adminDefaultCulture = null)
    {
        var settings = await sites.GetSettingsAsync<LocalizationSettings>();
        var crestSettings = await sites.GetSettingsAsync<CrestLocalizationSettings>();
        cultures ??= await localizationService.GetSupportedCulturesAsync();
        defaultCulture ??= await localizationService.GetDefaultCultureAsync();
        fallBackToParentCulture ??= settings.FallBackToParentCulture;
        adminDefaultCulture ??= crestSettings.AdminDefaultCulture;

        return new CrestLocalization(
            defaultCulture,
            cultures,
            fallBackToParentCulture.Value,
            adminDefaultCulture,
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
    // Rung 3 of the client resolution chain (plans/user-localization.md's "Resolution
    // architecture") - a tenant-level default distinct from DefaultCulture above, only
    // consulted by the client when the current route is under the admin path prefix. Null
    // means "no admin-specific override" - the client falls through to rung 4/5 as if this
    // setting didn't exist.
    string? AdminDefaultCulture,
    CrestCulture[] AvailableCultures);

// Crest-owned, separate from OrchardCore's own LocalizationSettings - AdminDefaultCulture
// is a Crest concept upstream OrchardCore has no equivalent for.
public sealed class CrestLocalizationSettings
{
    public string? AdminDefaultCulture { get; set; }
}

// Culture is null when the user has no stored default (falls through to the next
// resolution step — see plans/user-localization.md's resolution order).
public sealed record CrestUserCulture(string? Culture);

public sealed record CrestCulture(string Value, string Label, string NativeLabel)
{
    public static CrestCulture From(CultureInfo culture) => new(
        culture.Name,
        string.IsNullOrWhiteSpace(culture.DisplayName) ? culture.Name : culture.DisplayName,
        string.IsNullOrWhiteSpace(culture.NativeName) ? culture.DisplayName : culture.NativeName);
}
