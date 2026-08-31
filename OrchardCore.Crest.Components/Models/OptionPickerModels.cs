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

// NOTE: there is deliberately no client-side filter type ON THE QUERY PATH. When a
// picker asks for rows it identifies its attachment (content type + field) and
// reports what the user entered, never what the server should allow - a client that
// could describe its own query filters could query any source with any predicate.
// The ATTACHMENT models below are different: they are the admin settings editor's
// view of the per-field configuration the server itself stores and later reads
// (api/crest/option-picker/attachment, EditContentTypes-gated). Writing filters into
// the definition is exactly where filters are supposed to come from.

/// <summary>A field's current picker binding, as the settings editor reads it.</summary>
public sealed record OptionPickerAttachmentModel(
    bool FieldExists,
    string? FieldType,
    bool IsOptionPicker,
    OptionPickerSettingsModel Settings);

/// <summary>Mutable mirror of the server's OptionPickerFieldSettings, shaped for
/// direct binding in the settings editor.</summary>
public sealed class OptionPickerSettingsModel
{
    public string SourceKey { get; set; } = string.Empty;
    public bool Multiple { get; set; }
    public bool Required { get; set; }
    public string Placeholder { get; set; } = string.Empty;
    public List<OptionPickerColumnModel> Columns { get; set; } = [];
    public string? DisplayTemplate { get; set; }
    public List<string> SearchColumns { get; set; } = [];
    public List<string> SortColumns { get; set; } = [];
    public List<OptionPickerFilterModel> Filters { get; set; } = [];
}

/// <summary>Mutable mirror of the server's OptionColumn.</summary>
public sealed class OptionPickerColumnModel
{
    public string Path { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string? Width { get; set; }
    public bool Visible { get; set; } = true;
    public bool InChip { get; set; }
}

/// <summary>Mutable mirror of the server's OptionFilter. A non-blank ValueFrom makes
/// the filter dependent (cascading); Value is the literal alternative.</summary>
public sealed class OptionPickerFilterModel
{
    public string Path { get; set; } = string.Empty;
    public string Operator { get; set; } = "Equals";
    public string? Value { get; set; }
    public string? ValueFrom { get; set; }
    public bool RequireParentValue { get; set; } = true;
    public string OnParentChange { get; set; } = "WarnThenClear";
}

/// <summary>A column an option source can offer (api/crest/option-sources/{key}/columns).</summary>
public sealed record OptionSourceColumnModel(string Path, string Label, bool IsDefaultDisplay = false);

/// <summary>An content part list as the management screens see it.</summary>
public sealed record ContentPartListModel(
    string ContentItemId,
    string Key,
    string DisplayText,
    string Source,
    string DataLock,
    string EditLock,
    OptionModel[] Options);

/// <summary>A member of an content part list. Category is never null - options without an
/// assigned category carry "Uncategorized".</summary>
public sealed record OptionModel(
    string ContentItemId,
    string Key,
    string DisplayText,
    string Source,
    bool Hidden,
    int Position,
    string Category);
