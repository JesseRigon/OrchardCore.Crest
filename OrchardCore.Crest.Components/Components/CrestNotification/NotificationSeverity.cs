namespace Crest.Components.Primitives;

/// <summary>
/// Specifies the severity of a <see cref="Crest.Components.Primitives.CrestNotification" />. Severity changes the visual styling of the CrestNotification (icon and background color).
/// </summary>
public enum NotificationSeverity
{
    /// <summary>
    /// Represents an error.
    /// </summary>
    Error,

    /// <summary>
    /// Represents some generic information.
    /// </summary>
    Info,

    /// <summary>
    /// Represents a success.
    /// </summary>
    Success,

    /// <summary>
    /// Represents a warning.
    /// </summary>
    Warning
}

