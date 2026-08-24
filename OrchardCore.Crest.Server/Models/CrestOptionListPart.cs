using OrchardCore.ContentManagement;

namespace Crest.Models;

/// <summary>
/// Marks a content item as an Option List - Crest's tenant-editable enum set.
/// </summary>
/// <remarks>
/// Deliberately parallel to Orchard's Taxonomy rather than built on it. Taxonomies
/// are a CMS categorization feature: the stock type is routable (AliasPart +
/// AutoroutePart, term pages, Liquid shapes) and everything carrying TaxonomyPart
/// shows up under Configuration > Taxonomies. Configuration data wants none of that,
/// and "taxonomy" is jargon to tenants - so this part carries its own option storage
/// and Crest takes no dependency on OrchardCore.Taxonomies.
/// </remarks>
public class CrestOptionListPart : ContentPart
{
    /// <summary>
    /// The set's stable logical key (e.g. "pricing.modifier-kind"). Module code
    /// resolves sets by this key, never by ContentItemId - ids are generated per
    /// tenant, so the same seeded set has a different id in every tenant.
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Where this set came from: <see cref="CrestOptionSources"/>.</summary>
    public string Source { get; set; } = CrestOptionSources.Tenant;

    /// <summary>The content type its options are created as.</summary>
    public string OptionContentType { get; set; } = string.Empty;

    /// <summary>The options themselves, contained in this item's own document -
    /// one read, no separate lifecycle.</summary>
    public List<ContentItem> Options { get; set; } = [];
}
