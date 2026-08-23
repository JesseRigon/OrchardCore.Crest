using System.Globalization;
using Crest.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.DataLocalization.Services;
using OrchardCore.Environment.Shell;
using OrchardCore.Entities;
using OrchardCore.Localization;
using OrchardCore.Localization.Models;
using OrchardCore.Localization.Services;
using OrchardCore.Settings;
using OrchardCore.Users;
using OrchardCore.Users.Localization.Models;
using OrchardCore.Users.Models;

namespace Crest.ViewModels;

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
