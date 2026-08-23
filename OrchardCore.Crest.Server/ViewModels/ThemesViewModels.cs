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

namespace Crest.ViewModels;

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
