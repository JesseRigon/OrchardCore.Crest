using Crest.Navigation;
using Crest.Settings;
using OrchardCore.ContentManagement.Metadata.Builders;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.ContentManagement.Metadata.Settings;
using Xunit;

namespace Crest.Tests;

// The Content-menu designation: which types get an entry, and which permission
// gates it. The menu emission itself is builder plumbing verified live.
public class ContentTypesMenuNavigationProviderTests
{
    private static ContentTypeDefinition Type(bool? showInMenu = null, bool securable = false)
    {
        var builder = new ContentTypeDefinitionBuilder().WithName("Blog").WithDisplayName("Blog");

        if (showInMenu is not null)
        {
            builder.MergeSettings<CrestContentTypeMenuSettings>(settings => settings.ShowInContentMenu = showInMenu.Value);
        }

        if (securable)
        {
            builder.MergeSettings<ContentTypeSettings>(settings => settings.Securable = true);
        }

        return builder.Build();
    }

    [Fact]
    public void Undesignated_types_stay_out_of_the_menu()
    {
        Assert.False(ContentTypesMenuNavigationProvider.ShowsInContentMenu(Type()));
        Assert.False(ContentTypesMenuNavigationProvider.ShowsInContentMenu(Type(showInMenu: false)));
    }

    [Fact]
    public void Designated_types_show()
        => Assert.True(ContentTypesMenuNavigationProvider.ShowsInContentMenu(Type(showInMenu: true)));

    [Fact]
    public void Non_securable_types_gate_on_global_ListContent()
        => Assert.Equal("ListContent", ContentTypesMenuNavigationProvider.SelectPermission(Type(showInMenu: true)).Name);

    [Fact]
    public void Securable_types_gate_on_their_dynamic_permission()
    {
        var permission = ContentTypesMenuNavigationProvider.SelectPermission(Type(showInMenu: true, securable: true));

        Assert.Equal("ListContent_Blog", permission.Name);
        // The dynamic permission stays implied by the global one, so an admin with
        // plain ListContent still sees the entry.
        Assert.Contains(permission.ImpliedBy, implied => implied.Name == "ListContent");
    }
}
