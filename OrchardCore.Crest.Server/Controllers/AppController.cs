using Crest.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.Admin.Models;
using OrchardCore.Environment.Extensions;
using OrchardCore.Environment.Shell;
using OrchardCore.Environment.Shell.Descriptor;
using OrchardCore.Entities;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Navigation;
using OrchardCore.Settings;
using OrchardCore.Users;
using OrchardCore.Users.Localization.Models;
using OrchardCore.Users.Models;
using OrchardCore.Users.Services;
using OrchardCore.Localization;
using OrchardCore.Localization.Services;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Crest.Controllers;

[ApiController]
[AutoValidateAntiforgeryToken]
[Route("api/crest/app")]
public sealed class AppController(
    ShellSettings shellSettings,
    IShellHost shellHost,
    IShellDescriptorManager shellDescriptorManager,
    IExtensionManager extensionManager,
    ISiteService siteService,
    INavigationManager navigationManager,
    IOptions<AdminOptions> adminOptions,
    IAuthorizationService authorization,
    CrestAdminMenuLayoutService layoutService,
    CrestPrimaryNavMenuSettingsStore primaryNavMenuSettingsStore,
    CrestAdminSettingsNormalizer adminSettingsNormalizer,
    CrestTitleBarSettingsStore titleBarSettingsStore,
    CrestIconController iconController,
    CrestRouteAuthorizationService routeAuthorization,
    CrestProfileMenuService profileMenuService,
    UserManager<IUser> userManager,
    IServiceProvider serviceProvider) : ControllerBase
{
    [HttpGet("manifest")]
    public async Task<ActionResult<AppManifest>> GetManifest()
    {
        if (!await authorization.AuthorizeAsync(User, AdminPermissions.AccessAdminPanel)) return Forbid();
        var descriptor = await shellDescriptorManager.GetShellDescriptorAsync();
        var site = await siteService.GetSiteSettingsAsync();
        var adminSettings = await adminSettingsNormalizer.EnsureNewMenuEnabledAsync();
        var titleBarSettings = await titleBarSettingsStore.GetAsync(HttpContext.RequestAborted);
        var featureIds = descriptor.Features.Select(feature => feature.Id).Order(StringComparer.Ordinal).ToArray();
        var featureInfos = extensionManager.GetFeatures(featureIds.AsEnumerable()).ToDictionary(feature => feature.Id);
        var tenants = await GetAvailableTenantsAsync();
        var adminItems = await navigationManager.BuildMenuAsync("admin", ControllerContext);
        var userDefaultCulture = await GetUserDefaultCultureAsync();
        var adminDefaultCulture = site.As<CrestLocalizationSettings>().AdminDefaultCulture;
        var cultureSelector = await CultureSelector.FromAsync(HttpContext, shellSettings, serviceProvider.GetService<ILocalizationService>(), userDefaultCulture, adminDefaultCulture);
        var profileMenu = await profileMenuService.BuildAsync(User, HttpContext.RequestAborted);
        // Same caption resolution as NavigationController.GetMenu - the manifest's copy of the
        // admin menu must agree with the sidebar endpoint for the same request culture.
        var captionResolver = serviceProvider.GetRequiredService<CrestMenuCaptionResolver>();
        await captionResolver.EnsureLoadedAsync();
        var adminMenu = await layoutService.ApplyAsync(new NavigationMenu("admin", adminItems.OrderBy(item => item.Position, NavigationPositionComparer.Instance)
            .Select(item => NavigationItem.From(item, captionResolver))
            .ToArray()));
        adminMenu = adminMenu with { PrimaryNavMenuSettings = await primaryNavMenuSettingsStore.GetAsync(HttpContext.RequestAborted) };
        adminMenu = await iconController.ResolveMenuIconsAsync(
            adminMenu,
            CrestIconController.AdminMenuChromeIconKeys
                .Concat(cultureSelector.Cultures.Select(culture => culture.Icon))
                .Concat([
                    "iconify.mdi/current/default/check",
                    "iconify.mdi/current/default/weather-night",
                    "iconify.mdi/current/default/weather-sunny",
                ]),
            HttpContext.RequestAborted);

        return Ok(new AppManifest(
            Tenant.From(shellSettings),
            tenants,
            SiteSettings.From(site),
            AdminSettingsDto.From(adminSettings),
            CrestTitleBarSettingsDto.From(titleBarSettings),
            new AdminDescriptor(NormalizeAdminPath(adminOptions.Value.AdminUrlPrefix)),
            descriptor.SerialNumber,
            ComputeFeatureHash(descriptor.SerialNumber, featureIds),
            featureIds.Select(id => Feature.From(id, featureInfos.GetValueOrDefault(id))).ToArray(),
            adminMenu,
            await routeAuthorization.GetAuthorizedRoutesAsync(User),
            cultureSelector,
            profileMenu));
    }

    private async Task<string?> GetUserDefaultCultureAsync()
    {
        if (await userManager.GetUserAsync(User) is not User user)
        {
            return null;
        }

        user.TryGet<UserLocalizationSettings>(out var settings);
        return settings?.Culture;
    }

    private async Task<Tenant[]> GetAvailableTenantsAsync()
    {
        var userName = User.Identity?.Name;
        if (string.IsNullOrWhiteSpace(userName))
        {
            return [Tenant.From(shellSettings)];
        }

        var tenants = new List<Tenant>();
        foreach (var settings in shellHost.GetAllSettings().OrderBy(settings => settings.Name, StringComparer.OrdinalIgnoreCase))
        {
            if (string.Equals(settings.Name, shellSettings.Name, StringComparison.OrdinalIgnoreCase))
            {
                tenants.Add(Tenant.From(settings));
                continue;
            }

            try
            {
                await (await shellHost.GetScopeAsync(settings)).UsingServiceScopeAsync(async scope =>
                {
                    var userService = scope.ServiceProvider.GetService<IUserService>();
                    if (userService is not null && await userService.GetUserAsync(userName) is not null)
                    {
                        tenants.Add(Tenant.From(settings));
                    }
                });
            }
            catch
            {
                // Ignore tenants that are unavailable or do not have Users enabled.
            }
        }

        return tenants
            .DistinctBy(tenant => tenant.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string NormalizeAdminPath(string? adminUrlPrefix)
    {
        var prefix = string.IsNullOrWhiteSpace(adminUrlPrefix) ? "admin" : adminUrlPrefix.Trim('/');
        return '/' + prefix;
    }

    private static string ComputeFeatureHash(int serialNumber, IEnumerable<string> featureIds)
    {
        var input = $"{serialNumber}:{string.Join('|', featureIds)}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input))).ToLowerInvariant();
    }
}

public sealed record AppManifest(
    Tenant Tenant,
    Tenant[] Tenants,
    SiteSettings Site,
    AdminSettingsDto AdminSettings,
    CrestTitleBarSettingsDto TitleBarSettings,
    AdminDescriptor Admin,
    int FeatureSerialNumber,
    string FeatureHash,
    Feature[] Features,
    NavigationMenu AdminMenu,
    CrestRouteAccess[] AuthorizedRoutes,
    CultureSelector CultureSelector,
    NavigationMenu ProfileMenu);

public sealed record CultureSelector(
    string? UserDefaultCulture,
    string TenantDefaultCulture,
    // Rung 3 - see CrestLocalizationSettings.AdminDefaultCulture (LocalizationController.cs).
    // Null means no admin-specific override is configured for this tenant.
    string? AdminDefaultCulture,
    CultureOption[] Cultures,
    string CookieName,
    string CookiePath)
{
    // The server does NOT resolve culture - it has no way to. The session override (rung 1
    // of the priority chain) lives only in the browser's sessionStorage, which the server
    // can never see; only the client can weigh it against everything else. This type is
    // deliberately just raw inputs - the tenant's supported cultures + default, and the
    // signed-in user's stored default (if any) - for DisplayManager.ResolveCultureAsync to
    // resolve from. See plans/user-localization.md's "Resolution architecture" section.
    // (Earlier revisions of this type also carried a server-computed CurrentCulture field -
    // removed, since its mere presence invited reading it as an authoritative answer even
    // though nothing ever consumed it that way.)
    public static async Task<CultureSelector> FromAsync(
        HttpContext httpContext,
        ShellSettings shellSettings,
        ILocalizationService? localizationService,
        string? userDefaultCulture,
        string? adminDefaultCulture)
    {
        var supportedCultures = localizationService is null
            ? []
            : await localizationService.GetSupportedCulturesAsync();
        var tenantDefaultCulture = localizationService is null
            ? CultureInfo.CurrentUICulture.Name
            : await localizationService.GetDefaultCultureAsync();

        return new CultureSelector(
            userDefaultCulture,
            tenantDefaultCulture,
            adminDefaultCulture,
            supportedCultures
                .Select(CultureInfo.GetCultureInfo)
                .Select(culture => new CultureOption(culture.Name, culture.NativeName, GetIcon(culture)))
                .ToArray(),
            CrestCultureCookie.MakeCookieName(shellSettings),
            CrestCultureCookie.MakeCookiePath(httpContext));
    }

    private static string GetIcon(CultureInfo culture)
    {
        var region = culture.Name.Split('-', StringSplitOptions.RemoveEmptyEntries).LastOrDefault(part => part.Length == 2);
        region ??= culture.TwoLetterISOLanguageName switch
        {
            "en" => "us", "pt" => "pt", "zh" => "cn", "ar" => "sa", _ => null,
        };

        // Country flags are normal Iconify references, so they go through the
        // same server-resolved pack as every other Crest icon.
        return region is null
            ? "iconify.mdi/current/default/translate"
            : $"iconify.circle-flags/current/default/{region.ToLowerInvariant()}";
    }
}

public sealed record CultureOption(string Value, string Label, string Icon);

public sealed record Tenant(
    string Name,
    string TenantId,
    string State,
    string? RequestUrlHost,
    string[] RequestUrlHosts,
    string? RequestUrlPrefix)
{
    public static Tenant From(ShellSettings settings) => new(
        settings.Name,
        settings.TenantId,
        settings.State.ToString(),
        settings.RequestUrlHost,
        settings.RequestUrlHosts ?? [],
        settings.RequestUrlPrefix);
}

public sealed record AdminDescriptor(string BasePath);

public sealed record AdminSettingsDto(
    bool DisplayThemeToggler,
    bool DisplayMenuFilter,
    bool DisplayNewMenu,
    bool DisplayTitlesInTopbar)
{
    public static AdminSettingsDto From(AdminSettings settings) => new(
        settings.DisplayThemeToggler,
        settings.DisplayMenuFilter,
        settings.DisplayNewMenu,
        settings.DisplayTitlesInTopbar);
}

public sealed record CrestTitleBarSettingsDto(
    bool DisplayCultureLabel,
    string? TenantAvatarImageUrl,
    string TenantAvatarShape,
    string? TenantAvatarClipPath,
    string? TenantAvatarBorderRadius)
{
    public static CrestTitleBarSettingsDto From(CrestTitleBarSettings settings) => new(
        settings.DisplayCultureLabel,
        settings.TenantAvatarImageUrl,
        settings.TenantAvatarShape,
        settings.TenantAvatarClipPath,
        settings.TenantAvatarBorderRadius);
}
