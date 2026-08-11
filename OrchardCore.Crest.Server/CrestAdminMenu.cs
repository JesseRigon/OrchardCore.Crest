using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.Navigation;
using OrchardCore.Settings;

namespace Crest;

// Stock OrchardCore admin menu providers use .Action(...), which MVC's
// IUrlHelper/LinkGenerator resolves against the tenant's real, configured
// AdminOptions.AdminUrlPrefix automatically - e.g. AdminUrlPrefix "backoffice"
// replaces the literal word "Admin" in every URL (see
// AdminAreaControllerRouteMapper upstream), it isn't layered on top of it. Design
// System/Icons have no MVC controller action to route to (Blazor-only pages), so
// .Url(...) is the only option - building it from the real AdminOptions.AdminUrlPrefix
// keeps these two links correct under a custom prefix, matching the same
// "{realAdminPrefix}/DesignSystem" shape stock links get. DesignSystem.razor/
// Icons.razor's own "@page" directives carry no "/Admin" segment either - Blazor's
// Router resolves them relative to <base href>, already rewritten to the real
// prefix by the admin document root (Crest.Server/Components/App.razor).
public sealed class CrestAdminMenu(IStringLocalizer<CrestAdminMenu> stringLocalizer, IOptions<AdminOptions> adminOptions) : AdminNavigationProvider
{
    private readonly IStringLocalizer S = stringLocalizer;

    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        var adminPath = "/" + adminOptions.Value.AdminUrlPrefix.Trim('/');

        builder.Add(S["Design"], NavigationConstants.AdminMenuDesignPosition, design => design
            .AddClass("design")
            .Id("design")
            .Add(S["Design System"], S["Design System"].PrefixPosition(), designSystem => designSystem
                .AddClass("design-system")
                .AddClass("icon-class-@iconify:mdi:palette")
                .Id("design-system")
                .Url($"{adminPath}/DesignSystem")
                .Permission(SettingsPermissions.ManageSettings)
                .LocalNav()
            )
            .Add(S["Icons"], S["Icons"].PrefixPosition(), icons => icons
                .AddClass("icons")
                .AddClass("icon-class-@iconify:mdi:shape")
                .Id("icons")
                .Url($"{adminPath}/Design/Icons")
                .Permission(SettingsPermissions.ManageSettings)
                .LocalNav()
            ), priority: 1);

        return ValueTask.CompletedTask;
    }
}
