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

    /// <summary>Where this option came from: <see cref="CrestOptionSources"/>.
    /// Version history shows THAT a module option was edited, but cannot tell
    /// module-shipped from tenant-created - hence this explicit field.</summary>
    public string Source { get; set; } = CrestOptionSources.Tenant;

    /// <summary>Suppressed from pickers without being deleted, so historical content
    /// referencing this option stays resolvable.</summary>
    public bool Hidden { get; set; }

    /// <summary>Sort order within the set; ties fall back to display text.</summary>
    public int Position { get; set; }

    /// <summary>Back-reference to the owning Option List content item.</summary>
    public string OptionListContentItemId { get; set; } = string.Empty;
}

/// <summary>Provenance values for <see cref="CrestOptionPart.Source"/> and
/// <see cref="CrestOptionListPart.Source"/>.</summary>
public static class CrestOptionSources
{
    /// <summary>Seeded by a module. Tenants may relabel or hide these, but the key
    /// is the module's contract.</summary>
    public const string Module = "Module";

    /// <summary>Created by the tenant.</summary>
    public const string Tenant = "Tenant";
}
