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
public class CrestContentPartListPart : ContentPart
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
    /// <remarks>
    /// There is deliberately NO sort setting on the list. Sort is an INSTANCE
    /// parameter: the same list renders categorized in one picker and key-ordered in
    /// another (OptionPickerFieldSettings.SortColumns / the query's SortColumns), and
    /// that applies to every option source, foreign-key ones included. What the list
    /// itself owns is the manual order - each option's Position.
    /// </remarks>
    public string OptionContentType { get; set; } = string.Empty;

    /// <summary>
    /// Freezes the list's MACHINE surface: option categories and category assignments
    /// (code evaluates formulas against them). Labels, visibility, order, sort mode
    /// and tenant-added options stay editable - new options must use one of the
    /// list's existing categories. Value is who placed the lock:
    /// <see cref="CrestContentPartListLockSources"/>.
    /// </summary>
    public string DataLock { get; set; } = CrestContentPartListLockSources.None;

    /// <summary>
    /// Freezes ALL tenant editing - the list is read-only: no relabels, no
    /// hiding, no reordering, no new options, no deletion. (How an INSTANCE sorts the
    /// list for display is not list data and is never locked.) Value is who placed
    /// the lock: <see cref="CrestContentPartListLockSources"/>.
    /// </summary>
    public string EditLock { get; set; } = CrestContentPartListLockSources.None;

    /// <summary>The options themselves, contained in this item's own document -
    /// one read, no separate lifecycle.</summary>
    public List<ContentItem> Options { get; set; } = [];
}

/// <summary>
/// Who placed a lock - which decides who may lift it. One mechanism, two
/// authorities: a Module lock is the developer's contract and no one in the tenant
/// (super admin included) can change it; a Tenant lock is placed and lifted by the
/// tenant super admin (the LockContentPartLists permission) through the same system.
/// </summary>
public static class CrestContentPartListLockSources
{
    /// <summary>Not locked.</summary>
    public const string None = "None";

    /// <summary>Placed by the tenant super admin; the super admin can lift it.</summary>
    public const string Tenant = "Tenant";

    /// <summary>Declared by the owning module's seed; immutable within the tenant.</summary>
    public const string Module = "Module";
}

