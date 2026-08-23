using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using OrchardCore.Entities;
using OrchardCore.Environment.Shell;
using OrchardCore.Security;
using OrchardCore.Security.Options;
using OrchardCore.Security.Settings;
using OrchardCore.Settings;

namespace Crest.ViewModels;

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
