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

namespace Crest.ViewModels;

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
