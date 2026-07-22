using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using OrchardCore.Admin;
using OrchardCore.Contents;
using OrchardCore.Indexing;
using OrchardCore.Indexing.Core;
using OrchardCore.Localization;
using OrchardCore.Media;
using OrchardCore.Recipes;
using OrchardCore.Roles;
using OrchardCore.Security;
using OrchardCore.Security.Permissions;
using OrchardCore.Settings;
using OrchardCore.Users;

namespace Crest.Services;

/// <summary>
/// A batch, client-navigation projection of Orchard permissions. This is only
/// a UI gate: the underlying Orchard services and Crest adapters remain the
/// authority for every data operation.
/// </summary>
public sealed class CrestRouteAuthorizationService(IAuthorizationService authorization)
{
    private static readonly CrestRoutePermission[] Routes =
    [
        new("/Admin", AdminPermissions.AccessAdminPanel),
        new("/Admin/Features", OrchardCore.Features.FeaturesPermissions.ManageFeatures),
        new("/Admin/Themes", OrchardCore.Themes.Permissions.ApplyTheme),
        new("/Admin/AdminMenus", OrchardCore.AdminMenu.AdminMenuPermissions.ManageAdminMenu),
        new("/Admin/AdminMenu", OrchardCore.AdminMenu.AdminMenuPermissions.ManageAdminMenu),
        new("/Admin/AdminMenu/List", OrchardCore.AdminMenu.AdminMenuPermissions.ManageAdminMenu),
        new("/Admin/Menus", OrchardCore.Menu.Permissions.ManageMenu),
        new("/Admin/Menu", OrchardCore.Menu.Permissions.ManageMenu),
        new("/Admin/Menu/List", OrchardCore.Menu.Permissions.ManageMenu),
        new("/Admin/Contents/ContentItems/Menu", OrchardCore.Menu.Permissions.ManageMenu),
        new("/Admin/Contents/ContentItems", OrchardCore.Contents.CommonPermissions.ListContent),
        new("/Admin/Contents/ContentTypes/{ContentType}/Create", OrchardCore.Contents.CommonPermissions.EditContent),
        new("/Admin/Contents/ContentItems/{ContentItemId}/Edit", OrchardCore.Contents.CommonPermissions.EditContent),
        new("/Admin/ContentTypes/List", ContentTypesPermissions.ViewContentTypes),
        new("/Admin/ContentTypes/ListParts", ContentTypesPermissions.ViewContentTypes),
        new("/Admin/Users/Index", UsersPermissions.ListUsers),
        new("/Admin/Users/Create", UsersPermissions.EditUsers),
        new("/Admin/Users/Edit/{Id}", UsersPermissions.EditUsers),
        new("/Admin/Tenants", OrchardCore.Tenants.Permissions.ManageTenants),
        new("/Admin/Roles/Index", RolesPermissions.ManageRoles),
        new("/Admin/Media", MediaPermissions.ManageMedia),
        new("/Admin/Media/Options", MediaPermissions.ViewMediaOptions),
        new("/Admin/MediaProfiles", MediaPermissions.ManageMediaProfiles),
        new("/Admin/indexing", IndexingPermissions.ManageIndexes),
        new("/Admin/Queries/Index", OrchardCore.Queries.Permissions.ManageQueries),
        new("/Admin/Recipes", RecipePermissions.ManageRecipes),
        new("/Admin/Templates", OrchardCore.Templates.Permissions.ManageTemplates),
        new("/Admin/Settings/general", SettingsPermissions.ManageSettings),
        new("/Admin/Settings/admin", AdminPermissions.ManageAdminSettings),
        new("/Admin/Settings/localization", LocalizationPermissions.ManageCultures),
        new("/Admin/Settings/userLogin", UsersPermissions.ManageUsers),
        new("/Admin/Settings/SecurityHeaders", SecurityPermissions.ManageSecurityHeadersSettings),
        new("/Admin/Settings", SecurityPermissions.ManageSecurityHeadersSettings),
        new("/Admin/Design/Icons", SettingsPermissions.ManageSettings),
        new("/Admin/Icons", SettingsPermissions.ManageSettings),
        new("/Admin/DesignSystem", SettingsPermissions.ManageSettings),
    ];

    public async Task<CrestRouteAccess[]> GetAuthorizedRoutesAsync(ClaimsPrincipal user)
    {
        var granted = new List<CrestRouteAccess>();
        foreach (var route in Routes)
        {
            if (await authorization.AuthorizeAsync(user, route.Permission))
            {
                granted.Add(new CrestRouteAccess(route.Template));
            }
        }

        return granted.ToArray();
    }

    public async Task<bool> CanAccessAsync(ClaimsPrincipal user, string? path)
    {
        var route = Routes.FirstOrDefault(candidate => Matches(candidate.Template, path));
        return route is not null && await authorization.AuthorizeAsync(user, route.Permission);
    }

    public static bool Matches(string template, string? path)
    {
        var templateSegments = template.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        var pathSegments = (path ?? string.Empty).Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (templateSegments.Length != pathSegments.Length)
        {
            return false;
        }

        for (var index = 0; index < templateSegments.Length; index++)
        {
            var segment = templateSegments[index];
            if (segment.StartsWith('{') && segment.EndsWith('}'))
            {
                continue;
            }

            if (!string.Equals(segment, pathSegments[index], StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private sealed record CrestRoutePermission(string Template, Permission Permission);
}

public sealed record CrestRouteAccess(string Template);
