namespace Crest.Components.Models;

/// <summary>One row offered by an option source: an id plus the requested columns.</summary>
public sealed record OptionPickerRow(string Id, Dictionary<string, string?>? Values)
{
    /// <summary>The technical key, where the source has one distinct from the id.
    /// Null for entity sources whose id is their identity.</summary>
    public string? Key { get; init; }

    public bool Hidden { get; init; }
}

/// <summary>A column the picker displays. Mirrors the server-side field setting.</summary>
public sealed record OptionPickerColumn(
    string Path,
    string Label,
    string? Width = null,
    bool Visible = true,
    bool InChip = false);

// NOTE: there is deliberately no client-side filter type here. Restrictions live in
// the field's settings and are read SERVER-side; the picker identifies its attachment
// (content type + field) and reports what the user entered, never what the server
// should allow. A client that could describe its own filters could query any source
// with any predicate, including paths the dropdown never displays.

/// <summary>An option list as the management screens see it.</summary>
public sealed record OptionListModel(
    string ContentItemId,
    string Key,
    string DisplayText,
    string Source,
    OptionModel[] Options);

/// <summary>A member of an option list.</summary>
public sealed record OptionModel(
    string ContentItemId,
    string Key,
    string DisplayText,
    string Source,
    bool Hidden,
    int Position);
