using OrchardCore.ContentManagement;

namespace Crest.Models;

/// <summary>
/// A member of an Option List.
/// </summary>
/// <remarks>
/// Identity rule: content stores ContentItemId, CODE compares <see cref="Key"/>,
/// humans read DisplayText, <see cref="Source"/> gives provenance. DisplayText is the
/// tenant-editable, localizable label and must stay freely renamable without breaking
/// logic - renaming "Markup" to "Standard uplift" changes nothing in the code that
/// switches on the key.
/// </remarks>
public class CrestOptionPart : ContentPart
{
    /// <summary>The technical key code switches on. Unique within its set.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Optional SECOND machine datum for options whose key identifies them but does
    /// not carry the data code needs (a dial code on a phone-country option, a regex
    /// on a postal-format option). Null for pure enums - the key is their value.
    /// Machine surface: frozen under a data lock exactly like Category, and never a
    /// place for display text - labels stay freely renamable, this does not.
    /// </summary>
    public string? Value { get; set; }

    /// <summary>Where this option came from: <see cref="CrestOptionSources"/>.
    /// Version history shows THAT a module option was edited, but cannot tell
    /// module-shipped from tenant-created - hence this explicit field.</summary>
    public string Source { get; set; } = CrestOptionSources.Tenant;

    /// <summary>
    /// The label used when the option names more than one of something ("Boxes" to
    /// DisplayText's "Box"). Null or empty means the option has no distinct plural
    /// and readers fall back to DisplayText. Like DisplayText it is display-only,
    /// tenant-editable and never part of the machine surface.
    /// </summary>
    public string? DisplayTextPlural { get; set; }

    /// <summary>Suppressed from pickers without being deleted, so historical content
    /// referencing this option stays resolvable.</summary>
    public bool Hidden { get; set; }

    /// <summary>Sort order within the set; ties fall back to display text.</summary>
    public int Position { get; set; }

    /// <summary>
    /// Flat, single-valued grouping label used by the Categorized sort. Never null:
    /// an option without an explicit category belongs to
    /// <see cref="CrestOptionCategories.Uncategorized"/>. No hierarchy - a category
    /// is a label, not a tree.
    /// </summary>
    public string Category { get; set; } = CrestOptionCategories.Uncategorized;

    /// <summary>Back-reference to the owning Option List content item.</summary>
    public string ContentPartListContentItemId { get; set; } = string.Empty;
}

/// <summary>Provenance values for <see cref="CrestOptionPart.Source"/> and
/// <see cref="CrestContentPartListPart.Source"/>.</summary>
public static class CrestOptionSources
{
    /// <summary>Seeded by a module. Tenants may relabel or hide these, but the key
    /// is the module's contract.</summary>
    public const string Module = "Module";

    /// <summary>Created by the tenant.</summary>
    public const string Tenant = "Tenant";
}

/// <summary>Category values for <see cref="CrestOptionPart.Category"/>.</summary>
public static class CrestOptionCategories
{
    /// <summary>The default category every option belongs to until a tenant assigns
    /// one. Stored as this invariant token; screens localize it for display.</summary>
    public const string Uncategorized = "Uncategorized";
}
