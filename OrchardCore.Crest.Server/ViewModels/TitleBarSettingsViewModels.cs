using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrchardCore.Admin;
using Crest.Services;

namespace Crest.ViewModels;

public sealed record CrestTitleBarSettingsUpdate(
    bool DisplayCultureLabel,
    string? TenantAvatarImageUrl,
    string TenantAvatarShape,
    string? TenantAvatarClipPath,
    string? TenantAvatarBorderRadius);
