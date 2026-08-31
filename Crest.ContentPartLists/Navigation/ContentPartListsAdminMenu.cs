using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.Navigation;

namespace Crest.Navigation;

// Joins stock OrchardCore.ContentTypes' menu group - Design > Content Definition >
// Content Types / Content Parts - as a sibling entry, matching Orchard's
// content-definition nomenclature. Same .Url(...) rationale as Crest.Server's
// CrestAdminMenu: the screen is a Blazor page with no MVC action to .Action(...) to,
// so the link is built from the tenant's real AdminOptions.AdminUrlPrefix.
public sealed class ContentPartListsAdminMenu(IStringLocalizer<ContentPartListsAdminMenu> stringLocalizer, IOptions<AdminOptions> adminOptions) : AdminNavigationProvider
{
    private readonly IStringLocalizer S = stringLocalizer;

    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        var adminPath = "/" + adminOptions.Value.AdminUrlPrefix.Trim('/');

        builder.Add(S["Design"], design => design
            .Add(S["Content Definition"], S["Content Definition"].PrefixPosition(), contentDefinition => contentDefinition
                .Add(S["Content Part Lists"], S["Content Part Lists"].PrefixPosition("3"), contentPartLists => contentPartLists
                    .AddClass("content-part-lists")
                    .AddClass("icon-class-@iconify:mdi:format-list-bulleted")
                    .Id("content-part-lists")
                    .Url($"{adminPath}/ContentTypes/ContentPartLists")
                    .Permission(Crest.Permissions.CrestContentPartListPermissions.ViewContentPartLists)
                    .LocalNav()
                )
            ));

        return ValueTask.CompletedTask;
    }
}
