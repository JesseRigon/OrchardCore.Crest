using OrchardCore.ContentManagement.Metadata.Settings;

namespace Crest.Settings;

/// <summary>
/// Per-ATTACHMENT configuration for an <see cref="Crest.Fields.OptionPickerField"/>.
/// The projection lives here rather than on the field or the target type, because the
/// same source is displayed differently in different dropdowns.
/// </summary>
public class OptionPickerFieldSettings : FieldSettings
{
    /// <summary>Which source to draw from, e.g. "optionlist:pricing.modifier-kind".</summary>
    public string SourceKey { get; set; } = string.Empty;

    public bool Multiple { get; set; }

    public string Placeholder { get; set; } = string.Empty;

    /// <summary>The columns this dropdown displays, in order. Empty means the
    /// provider's default display column.</summary>
    public OptionColumn[] Columns { get; set; } = [];

    /// <summary>Optional composition of the selected value, e.g. "{Name} ({Badge})".
    /// Null or empty joins the visible columns instead.</summary>
    public string? DisplayTemplate { get; set; }

    /// <summary>Column paths the type-ahead filters on. Empty means the provider
    /// decides.</summary>
    public string[] SearchColumns { get; set; } = [];

    /// <summary>Column paths to sort by. Empty means the provider's natural order
    /// (for Option Lists: position, then display text).</summary>
    public string[] SortColumns { get; set; } = [];

    /// <summary>Restrictions applied to what the dropdown offers. Evaluated by the
    /// provider, which knows how to filter its own source.</summary>
    public OptionFilter[] Filters { get; set; } = [];
}

/// <summary>One displayed column of the referenced record.</summary>
public class OptionColumn
{
    /// <summary>How to pull the value from the source record. PROVIDER-INTERPRETED:
    /// each provider documents and advertises its own paths rather than sharing a
    /// universal path language.</summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>Column header in the picker.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>Display width hint (CSS length), optional.</summary>
    public string? Width { get; set; }

    /// <summary>Shown in the dropdown rows.</summary>
    public bool Visible { get; set; } = true;

    /// <summary>Shown in the collapsed/selected display.</summary>
    public bool InChip { get; set; }
}

/// <summary>A restriction on what a picker offers.</summary>
public class OptionFilter
{
    /// <summary>Provider-interpreted path, same vocabulary as
    /// <see cref="OptionColumn.Path"/>.</summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>See <see cref="OptionFilterOperators"/>.</summary>
    public string Operator { get; set; } = OptionFilterOperators.Equals;

    /// <summary>A literal value to compare against. Ignored when
    /// <see cref="ValueFrom"/> is set.</summary>
    public string? Value { get; set; }

    /// <summary>
    /// Reads the comparison value from the item being edited (e.g. "Customer.Party"),
    /// which is what makes dependent/cascading dropdowns work - "show only contacts
    /// belonging to the party chosen above". Requires the editor to supply the current
    /// item state when querying.
    /// </summary>
    public string? ValueFrom { get; set; }
}

public static class OptionFilterOperators
{
    public const string Equals = "Equals";
    public const string NotEquals = "NotEquals";
    public const string In = "In";
    public const string Contains = "Contains";
    public const string GreaterThan = "GreaterThan";
    public const string LessThan = "LessThan";
}
