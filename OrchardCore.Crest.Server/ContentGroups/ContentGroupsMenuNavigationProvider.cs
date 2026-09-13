using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.Contents;
using OrchardCore.Navigation;

namespace Crest.ContentGroups;

/// <summary>
/// When <see cref="CrestContentGroupsSettings.AutoMenuPages"/> is on: one Content-menu
/// entry per visible group, linking to the generic content-items page filtered by
/// <c>?group=</c>. Sits beside the per-type entries (same section, same route, same
/// materialized sync), so the tenant chooses by-type pages, by-group pages, or both.
/// </summary>
public sealed class ContentGroupsMenuNavigationProvider(
    CrestContentGroupService groups,
    IStringLocalizer<ContentGroupsMenuNavigationProvider> stringLocalizer,
    IOptions<AdminOptions> adminOptions) : AdminNavigationProvider
{
    private readonly IStringLocalizer S = stringLocalizer;

    protected override async ValueTask BuildAsync(NavigationBuilder builder)
    {
        if (!(await groups.GetSettingsAsync()).AutoMenuPages)
        {
            return;
        }

        var visible = (await groups.ListAsync()).Where(group => !group.Hidden && group.Entries.Count > 0).ToArray();
        if (visible.Length == 0)
        {
            return;
        }

        var adminPath = "/" + adminOptions.Value.AdminUrlPrefix.Trim('/');

        builder.Add(S["Content"], content =>
        {
            foreach (var group in visible)
            {
                // Tenant data, like a type's display name: the caption doubles as the
                // sync match key, so a rename re-keys the entry (documented boundary).
                var caption = new LocalizedString(group.DisplayName, group.DisplayName);

                content.Add(caption, caption.PrefixPosition(), item => item
                    .AddClass($"content-group-{group.Key.ToLowerInvariant()}")
                    .AddClass("icon-class-@iconify:mdi:folder-outline")
                    .Id($"content-group-{group.Key.ToLowerInvariant()}")
                    .Url($"{adminPath}/Contents/ContentItems?group={Uri.EscapeDataString(group.Key)}")
                    .Permission(CommonPermissions.ListContent)
                    .LocalNav());
            }
        });
    }
}
