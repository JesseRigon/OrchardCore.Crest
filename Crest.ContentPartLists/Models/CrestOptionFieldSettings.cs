namespace Crest.Settings;

/// <summary>
/// Per-field settings for a custom data field on an option content type. Custom
/// fields are created at runtime through the Content Parts screen; this setting is
/// how the admin BUILDING the list declares each field's lock semantics - per field,
/// like a standard content item - rather than having them hardwired into the
/// list-level DataLock.
/// </summary>
public class CrestOptionFieldSettings
{
    /// <summary>
    /// True marks the field as MACHINE surface: its values freeze when the list's
    /// data lock is active, exactly like Category and Value. False (the default)
    /// makes it display surface - freely editable under a data lock, frozen only by
    /// an edit lock, like the labels.
    /// </summary>
    public bool DataLocked { get; set; }
}
