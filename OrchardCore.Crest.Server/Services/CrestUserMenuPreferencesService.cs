using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using OrchardCore.Entities;
using OrchardCore.Users;
using OrchardCore.Users.Models;
using Crest.ViewModels;

namespace Crest.Services;

/// <summary>
/// The user's own admin-menu preferences, on User.Properties the way OrchardCore.Users.
/// Localization keeps UserLocalizationSettings there (plans/user-settings.md sketches
/// exactly this shape). Nothing in it is tenant-wide.
/// </summary>
public sealed class CrestUserMenuPreferences
{
    /// <summary>
    /// Served menu item keys (NavigationItem.Key - the synced node's UniqueId) the user has
    /// hidden for themselves. The tenant's layout overlay hides for everyone; this hides
    /// for one person, and a page that lists what the menu lists (All Parties' panes)
    /// honours it by the same key.
    /// </summary>
    public List<string> HiddenItemKeys { get; set; } = [];
}

/// <summary>
/// The per-user layer under the tenant-wide <see cref="CrestAdminMenuLayoutService"/>:
/// applied after it, on the same served tree, so a key hidden by the tenant never reaches
/// here and a key hidden by the user vanishes with its children. Self-service only - a
/// user edits nobody's preference but their own.
/// </summary>
public sealed class CrestUserMenuPreferencesService(UserManager<IUser> userManager)
{
    public async Task<CrestUserMenuPreferences> GetAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity?.IsAuthenticated != true)
        {
            return new CrestUserMenuPreferences();
        }

        var user = await userManager.GetUserAsync(principal) as User;
        if (user is null || !user.TryGet<CrestUserMenuPreferences>(out var preferences))
        {
            return new CrestUserMenuPreferences();
        }

        return preferences;
    }

    public async Task<CrestUserMenuPreferences?> SetHiddenItemKeysAsync(ClaimsPrincipal principal, IEnumerable<string> hiddenItemKeys)
    {
        var user = await userManager.GetUserAsync(principal) as User;
        if (user is null)
        {
            return null;
        }

        var keys = hiddenItemKeys
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Select(key => key.Trim())
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();

        user.Alter<CrestUserMenuPreferences>(preferences => preferences.HiddenItemKeys = keys);
        var result = await userManager.UpdateAsync(user);
        return result.Succeeded ? new CrestUserMenuPreferences { HiddenItemKeys = keys } : null;
    }

    public async Task<NavigationMenu> ApplyAsync(NavigationMenu menu, ClaimsPrincipal principal)
    {
        var preferences = await GetAsync(principal);
        if (preferences.HiddenItemKeys.Count == 0)
        {
            return menu;
        }

        var hidden = preferences.HiddenItemKeys.ToHashSet(StringComparer.Ordinal);
        return menu with { Items = Apply(menu.Items, hidden) };
    }

    private static NavigationItem[] Apply(NavigationItem[] items, HashSet<string> hidden) => items
        .Where(item => item.Key is null || !hidden.Contains(item.Key))
        .Select(item => item with { Items = Apply(item.Items, hidden) })
        .ToArray();
}
