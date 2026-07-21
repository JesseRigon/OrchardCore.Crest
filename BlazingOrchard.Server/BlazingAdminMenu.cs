using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;
using OrchardCore.Settings;

namespace BlazingOrchard;

public sealed class BlazingAdminMenu(IStringLocalizer<BlazingAdminMenu> stringLocalizer) : AdminNavigationProvider
{
    private readonly IStringLocalizer S = stringLocalizer;

    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder.Add(S["Design"], NavigationConstants.AdminMenuDesignPosition, design => design
            .AddClass("design")
            .Id("design")
            .Add(S["Design System"], S["Design System"].PrefixPosition(), designSystem => designSystem
                .AddClass("design-system")
                .AddClass("icon-class-@iconify:mdi:palette")
                .Id("design-system")
                .Url("/Admin/DesignSystem")
                .Permission(SettingsPermissions.ManageSettings)
                .LocalNav()
            )
            .Add(S["Icons"], S["Icons"].PrefixPosition(), icons => icons
                .AddClass("icons")
                .AddClass("icon-class-@iconify:mdi:shape")
                .Id("icons")
                .Url("/Admin/Design/Icons")
                .Permission(SettingsPermissions.ManageSettings)
                .LocalNav()
            ), priority: 1);

        return ValueTask.CompletedTask;
    }
}
