using Crest.Blazor.Models;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.Data.Migration;

namespace Crest.Blazor.Migrations;

// Registers CrestBlazorComponentPart as Attachable (same as WidgetsListPart's own
// migration) and defines a BlazorComponent content type carrying it, with a Widget
// stereotype so it's placeable into any WidgetsListPart zone alongside stock Widgets -
// no new tree/zone mechanism, reusing Orchard's existing one (see plans/blazor hybrid
// conversion.md, Phase 3a).
public sealed class CrestBlazorComponentMigrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    public CrestBlazorComponentMigrations(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    public async Task<int> CreateAsync()
    {
        await _contentDefinitionManager.AlterPartDefinitionAsync(nameof(CrestBlazorComponentPart), builder => builder
            .Attachable()
            .WithDescription("Places a registered Blazor component into a content tree, the same way a Widget is placed."));

        await _contentDefinitionManager.AlterTypeDefinitionAsync("BlazorComponent", type => type
            .WithPart(nameof(CrestBlazorComponentPart))
            .Stereotype("Widget"));

        return 1;
    }
}
