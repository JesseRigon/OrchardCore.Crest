namespace Crest.Components.Primitives;

/// <summary>
/// Specifies the behavior of <see cref="Crest.Components.Primitives.CrestProgressBar" /> or <see cref="Crest.Components.Primitives.CrestProgressBarCircular" />.
/// </summary>
public enum ProgressBarMode
{
    /// <summary>
    /// CrestProgressBar displays its value as a percentage range (0 to 100).
    /// </summary>
    Determinate,

    /// <summary>
    /// CrestProgressBar displays continuous animation.
    /// </summary>
    Indeterminate
}

