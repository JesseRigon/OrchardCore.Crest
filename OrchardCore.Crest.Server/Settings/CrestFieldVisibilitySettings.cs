namespace Crest.Settings;

/// <summary>
/// A visibility condition on ANY field the Crest editor renders: the field shows
/// only while a controlling field's value satisfies the condition ("Void reason
/// appears when Status is Voided"). Deliberately a FIELD setting, not a picker
/// setting - the dependent field can be of any type.
/// </summary>
/// <remarks>
/// Comparison values follow the identity rule: for an OptionPickerField parent the
/// stored ids resolve to option KEYS (through the parent's own source), so
/// <see cref="Keys"/> holds keys, never tenant-renamable labels and never per-tenant
/// ids. Scalar parents compare their stored value directly.
/// <para>
/// A hidden field's value is CLEARED ON SAVE (user ruling 2026-09-02): a document
/// whose condition turned false must not silently keep the stale dependent value -
/// an un-voided invoice keeping its void reason would be invisible bad data.
/// </para>
/// </remarks>
public class CrestFieldVisibilitySettings
{
    /// <summary>The controlling field: "Field" on the type's implicit part, or
    /// "Part.Field". Blank means the field is always visible.</summary>
    public string? Path { get; set; }

    /// <summary>See <see cref="CrestFieldVisibilityOperators"/>.</summary>
    public string Operator { get; set; } = CrestFieldVisibilityOperators.Any;

    /// <summary>The comparison keys for Equals/In. Ignored for Any.</summary>
    public string[] Keys { get; set; } = [];

    public bool HasCondition => !string.IsNullOrWhiteSpace(Path);
}

public static class CrestFieldVisibilityOperators
{
    /// <summary>Visible once the controlling field has ANY value.</summary>
    public const string Any = "Any";

    /// <summary>Visible while the controlling field's resolved value matches one of
    /// the configured keys. (Equals and In are the same test - a single-select
    /// parent just has one resolved value.)</summary>
    public const string Equals = "Equals";

    /// <inheritdoc cref="Equals"/>
    public const string In = "In";

    public static readonly string[] All = [Any, Equals, In];
}
