using Crest.Models;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.Data.Migration;

namespace Crest.Migrations;

// Option Lists - Crest's tenant-editable enum system.
//
// FULLY PARALLEL to Orchard's Taxonomy/Term, sharing no parts with it. Stock Taxonomy
// is a CMS categorization feature: routable (AliasPart + AutoroutePart, term pages,
// Liquid shapes), and everything carrying TaxonomyPart is listed under Configuration >
// Taxonomies. Configuration data wants none of that, and the word is jargon to
// tenants - so these types carry their own option storage, and Crest takes NO
// dependency on OrchardCore.Taxonomies.
//
// Nothing here modifies OrchardCore; the types are ordinary content types built from
// stock building blocks.
public sealed class CrestOptionListMigrations(IContentDefinitionManager contentDefinitionManager) : DataMigration
{
    public const string OptionListContentType = "OptionList";
    public const string OptionContentType = "Option";

    public async Task<int> CreateAsync()
    {
        await contentDefinitionManager.AlterPartDefinitionAsync(nameof(CrestOptionListPart), part => part
            .Attachable()
            .Reusable(false)
            .WithDisplayName("Option list")
            .WithDescription("A named, tenant-editable set of options that content types can reference."));

        await contentDefinitionManager.AlterPartDefinitionAsync(nameof(CrestOptionPart), part => part
            .Attachable()
            .Reusable(false)
            .WithDisplayName("Option")
            .WithDescription("A member of an option list. The key is what code matches on; the title is the editable label."));

        await contentDefinitionManager.AlterTypeDefinitionAsync(OptionContentType, type => type
            .DisplayedAs("Option")
            .Versionable()
            .Securable()
            // Options exist inside their list, never standalone: not Creatable, not
            // Listable - they are reached through their Option List.
            .WithPart("TitlePart", part => part.WithPosition("0"))
            .WithPart(nameof(CrestOptionPart), part => part.WithPosition("1")));

        await contentDefinitionManager.AlterTypeDefinitionAsync(OptionListContentType, type => type
            .DisplayedAs("Option List")
            .Creatable()
            .Listable()
            .Versionable()
            .Securable()
            // CrestOptionListPart carries the options themselves (contained in this
            // item's own document), so no taxonomy part is involved.
            .WithPart("TitlePart", part => part.WithPosition("0"))
            .WithPart(nameof(CrestOptionListPart), part => part.WithPosition("1")));

        return 2;
    }

    // Tenants created against the first pass have TaxonomyPart stored on their
    // OptionList definition. Content type definitions live in the database, so
    // rewriting CreateAsync alone would leave existing tenants coupled to the
    // Taxonomies module forever - this drops the part where it was already recorded.
    public async Task<int> UpdateFrom1Async()
    {
        await contentDefinitionManager.AlterTypeDefinitionAsync(OptionListContentType, type => type
            .RemovePart("TaxonomyPart"));

        return 2;
    }
}
