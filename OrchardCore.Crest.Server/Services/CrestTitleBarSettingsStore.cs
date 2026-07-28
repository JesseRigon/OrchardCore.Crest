using System.Text.Json;
using System.Text.Json.Nodes;
using OrchardCore.Settings;

namespace Crest.Services;

/// <summary>Tenant-scoped Crest title-bar preferences stored alongside Orchard site settings.</summary>
public sealed class CrestTitleBarSettingsStore(ISiteService siteService)
{
    private const string SettingsKey = "CrestTitleBar";

    public async ValueTask<CrestTitleBarSettings> GetAsync(CancellationToken cancellationToken = default)
    {
        var site = await siteService.LoadSiteSettingsAsync();
        return CrestTitleBarSettings.Normalize(site.Properties[SettingsKey]?.Deserialize<CrestTitleBarSettings>(JsonSerializerOptions.Web));
    }

    public async ValueTask<CrestTitleBarSettings> SaveAsync(CrestTitleBarSettings? settings, CancellationToken cancellationToken = default)
    {
        var normalized = CrestTitleBarSettings.Normalize(settings);
        var site = await siteService.LoadSiteSettingsAsync();
        site.Properties[SettingsKey] = JsonSerializer.SerializeToNode(normalized, JsonSerializerOptions.Web) ?? new JsonObject();
        await siteService.UpdateSiteSettingsAsync(site);
        return normalized;
    }
}

public sealed class CrestTitleBarSettings
{
    public bool DisplayCultureLabel { get; set; }
    public string? TenantAvatarImageUrl { get; set; }
    public string TenantAvatarShape { get; set; } = "Circle";
    public string? TenantAvatarClipPath { get; set; }
    public string? TenantAvatarBorderRadius { get; set; }

    public static CrestTitleBarSettings Normalize(CrestTitleBarSettings? value)
    {
        value ??= new CrestTitleBarSettings();
        var shape = value.TenantAvatarShape?.Trim();
        if (!new[] { "Circle", "RoundedSquare", "Square", "Custom" }.Contains(shape, StringComparer.OrdinalIgnoreCase))
        {
            shape = "Circle";
        }

        return new CrestTitleBarSettings
        {
            DisplayCultureLabel = value.DisplayCultureLabel,
            TenantAvatarImageUrl = NormalizeOptional(value.TenantAvatarImageUrl),
            TenantAvatarShape = shape,
            TenantAvatarClipPath = NormalizeOptional(value.TenantAvatarClipPath),
            TenantAvatarBorderRadius = NormalizeOptional(value.TenantAvatarBorderRadius),
        };
    }

    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
