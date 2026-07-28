namespace Crest.Components.Primitives;

/// <summary>
/// Supplies information about a <see cref="Crest.Components.Primitives.CrestDataGrid{TItem}.LoadSettings" /> event that is being raised.
/// </summary>
public class DataGridLoadSettingsEventArgs
{
    /// <summary>
    /// Gets or sets the settings.
    /// </summary>
    public DataGridSettings? Settings { get; set; }
}

