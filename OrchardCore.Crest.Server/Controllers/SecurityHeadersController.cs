using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using OrchardCore.Entities;
using OrchardCore.Environment.Shell;
using OrchardCore.Security;
using OrchardCore.Security.Options;
using OrchardCore.Security.Settings;
using OrchardCore.Settings;

namespace Crest.Controllers;

[ApiController]
[AutoValidateAntiforgeryToken]
[Route("api/crest/security-headers")]
public sealed class SecurityHeadersController(
    ISiteService sites,
    IShellReleaseManager releaseManager,
    IOptionsSnapshot<SecuritySettings> configuredSettings,
    IAuthorizationService authorization) : ControllerBase
{
    private static readonly HashSet<string> ContentSecurityPolicyNames =
    [
        ContentSecurityPolicyValue.BaseUri, ContentSecurityPolicyValue.ChildSource,
        ContentSecurityPolicyValue.ConnectSource, ContentSecurityPolicyValue.DefaultSource,
        ContentSecurityPolicyValue.FontSource, ContentSecurityPolicyValue.FormAction,
        ContentSecurityPolicyValue.FrameAncestors, ContentSecurityPolicyValue.FrameSource,
        ContentSecurityPolicyValue.ImageSource, ContentSecurityPolicyValue.ManifestSource,
        ContentSecurityPolicyValue.MediaSource, ContentSecurityPolicyValue.ObjectSource,
        ContentSecurityPolicyValue.ScriptSource, ContentSecurityPolicyValue.StyleSource,
        ContentSecurityPolicyValue.Sandbox, ContentSecurityPolicyValue.UpgradeInsecureRequests,
    ];

    private static readonly HashSet<string> PermissionsPolicyNames =
    [
        PermissionsPolicyValue.Accelerometer, PermissionsPolicyValue.AmbientLightSensor,
        PermissionsPolicyValue.Autoplay, PermissionsPolicyValue.Battery, PermissionsPolicyValue.Camera,
        PermissionsPolicyValue.DisplayCapture, PermissionsPolicyValue.DocumentDomain,
        PermissionsPolicyValue.EncryptedMedia, PermissionsPolicyValue.FullScreen,
        PermissionsPolicyValue.GamePad, PermissionsPolicyValue.Geolocation, PermissionsPolicyValue.Gyroscope,
        PermissionsPolicyValue.LayoutAnimations, PermissionsPolicyValue.LegacyImageFormat,
        PermissionsPolicyValue.Magnetometer, PermissionsPolicyValue.Microphone, PermissionsPolicyValue.Midi,
        PermissionsPolicyValue.OversizedImages, PermissionsPolicyValue.Payment,
        PermissionsPolicyValue.PictureInPicture, PermissionsPolicyValue.PublicKeyRetrieval,
        PermissionsPolicyValue.SpeakerSelection, PermissionsPolicyValue.ScreenWakeLock,
        PermissionsPolicyValue.SyncXhr, PermissionsPolicyValue.UnoptimizedImages,
        PermissionsPolicyValue.UnsizedMedia, PermissionsPolicyValue.Usb, PermissionsPolicyValue.WebShare,
        PermissionsPolicyValue.WebXR,
    ];

    private static readonly HashSet<string> ReferrerPolicies =
    [
        ReferrerPolicyValue.NoReferrer, ReferrerPolicyValue.NoReferrerWhenDowngrade,
        ReferrerPolicyValue.Origin, ReferrerPolicyValue.OriginWhenCrossOrigin,
        ReferrerPolicyValue.SameOrigin, ReferrerPolicyValue.StrictOrigin,
        ReferrerPolicyValue.StrictOriginWhenCrossOrigin, ReferrerPolicyValue.UnsafeUrl,
    ];

    [HttpGet]
    public async Task<ActionResult<SecurityHeadersDto>> GetAsync()
    {
        if (!await authorization.AuthorizeAsync(User, SecurityPermissions.ManageSecurityHeadersSettings)) return Forbid();
        var settings = await sites.GetSettingsAsync<SecuritySettings>();
        return Ok(SecurityHeadersDto.From(settings, configuredSettings.Value.FromConfiguration));
    }

    [HttpPut]
    public async Task<ActionResult<SecurityHeadersDto>> SaveAsync([FromBody] SecurityHeadersDto dto)
    {
        if (!await authorization.AuthorizeAsync(User, SecurityPermissions.ManageSecurityHeadersSettings)) return Forbid();

        var contentSecurityPolicy = NormalizeContentSecurityPolicy(dto.ContentSecurityPolicy);
        var permissionsPolicy = NormalizePermissionsPolicy(dto.PermissionsPolicy);
        var referrerPolicy = ReferrerPolicies.Contains(dto.ReferrerPolicy ?? string.Empty)
            ? dto.ReferrerPolicy!
            : SecurityHeaderDefaults.ReferrerPolicy;

        var site = await sites.LoadSiteSettingsAsync();
        SecuritySettings? updated = null;
        site.Alter<SecuritySettings>(settings =>
        {
            settings.ContentTypeOptions = SecurityHeaderDefaults.ContentTypeOptions;
            settings.ContentSecurityPolicy = contentSecurityPolicy;
            settings.PermissionsPolicy = permissionsPolicy;
            settings.ReferrerPolicy = referrerPolicy;
            updated = settings;
        });
        await sites.UpdateSiteSettingsAsync(site);
        releaseManager.RequestRelease();

        return Ok(SecurityHeadersDto.From(updated!, configuredSettings.Value.FromConfiguration));
    }

    private static Dictionary<string, string> NormalizeContentSecurityPolicy(Dictionary<string, string>? values)
    {
        values ??= [];
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var name in ContentSecurityPolicyNames)
        {
            if (!values.TryGetValue(name, out var value)) continue;
            if (name is ContentSecurityPolicyValue.Sandbox or ContentSecurityPolicyValue.UpgradeInsecureRequests)
            {
                result[name] = null!;
            }
            else if (!string.IsNullOrWhiteSpace(value))
            {
                result[name] = value.Trim();
            }
        }
        return result;
    }

    private static Dictionary<string, string> NormalizePermissionsPolicy(Dictionary<string, string>? values)
    {
        values ??= [];
        return values
            .Where(pair => PermissionsPolicyNames.Contains(pair.Key))
            .Select(pair => new KeyValuePair<string, string>(pair.Key, pair.Value?.Trim() ?? string.Empty))
            .Where(pair => pair.Value is PermissionsPolicyOriginValue.Any or PermissionsPolicyOriginValue.Self ||
                pair.Value.StartsWith(PermissionsPolicyOriginValue.Self + " ", StringComparison.Ordinal))
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
    }
}

public sealed record SecurityHeadersDto(
    Dictionary<string, string>? ContentSecurityPolicy,
    Dictionary<string, string>? PermissionsPolicy,
    string? ReferrerPolicy,
    bool FromConfiguration)
{
    public static SecurityHeadersDto From(SecuritySettings settings, bool fromConfiguration) => new(
        settings.ContentSecurityPolicy,
        settings.PermissionsPolicy,
        settings.ReferrerPolicy,
        fromConfiguration);
}
