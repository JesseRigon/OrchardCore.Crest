using Crest.Settings;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.Contents;
using OrchardCore.Contents.Security;
using OrchardCore.Navigation;

namespace Crest.Navigation;

/// <summary>
/// One Content-menu entry per content type designated with
/// <see cref="CrestContentTypeMenuSettings.ShowInContentMenu"/>, linking to the
/// generic content-items page filtered to that type. The type filter travels as a
/// query string rather than a route segment so the existing
/// <c>/Contents/ContentItems</c> route registration (and its ListContent gate)
/// covers the filtered view — route matching is query-invisible.
/// </summary>
public sealed class ContentTypesMenuNavigationProvider(
    IContentDefinitionManager contentDefinitionManager,
    IStringLocalizer<ContentTypesMenuNavigationProvider> stringLocalizer,
    IOptions<AdminOptions> adminOptions) : AdminNavigationProvider
{
    private readonly IStringLocalizer S = stringLocalizer;

    internal static bool ShowsInContentMenu(ContentTypeDefinition definition) =>
        definition.GetSettings<CrestContentTypeMenuSettings>()?.ShowInContentMenu == true;

    /// <summary>
    /// Securable types carry their registered dynamic permission (still implied by
    /// global ListContent, so ordinary admins keep seeing the entry); other types
    /// keep the global one — their dynamic name is unregistered, and an
    /// unresolvable name would silently drop the gate entirely.
    /// </summary>
    internal static OrchardCore.Security.Permissions.Permission SelectPermission(ContentTypeDefinition definition) =>
        definition.IsSecurable()
            ? ContentTypePermissionsHelper.CreateDynamicPermission(
                ContentTypePermissionsHelper.PermissionTemplates[CommonPermissions.ListContent.Name], definition)
            : CommonPermissions.ListContent;

    protected override async ValueTask BuildAsync(NavigationBuilder builder)
    {
        var flagged = (await contentDefinitionManager.ListTypeDefinitionsAsync())
            .Where(ShowsInContentMenu)
            .OrderBy(definition => definition.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (flagged.Length == 0)
        {
            return;
        }

        var adminPath = "/" + adminOptions.Value.AdminUrlPrefix.Trim('/');

        builder.Add(S["Content"], content =>
        {
            foreach (var definition in flagged)
            {
                // The caption literal is the type's display name — it is tenant
                // data, so there is no PO resource to key it by. It doubles as
                // the sync match key (under "Content"), so a display-name change
                // re-keys the entry; accepted boundary recorded in the plan.
                var caption = new LocalizedString(definition.DisplayName, definition.DisplayName);

                var permission = SelectPermission(definition);

                content.Add(caption, caption.PrefixPosition(), item => item
                    .AddClass($"content-type-{definition.Name.ToLowerInvariant()}")
                    .AddClass("icon-class-@iconify:mdi:file-document-outline")
                    .Id($"content-type-{definition.Name.ToLowerInvariant()}")
                    .Url($"{adminPath}/Contents/ContentItems?type={Uri.EscapeDataString(definition.Name)}")
                    .Permission(permission)
                    .LocalNav());
            }
        });
    }
}
