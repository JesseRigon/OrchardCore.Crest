namespace Crest.Components.Primitives;

/// <summary>
/// Specifies the ways a <see cref="Crest.Components.Primitives.CrestTimeline" /> component renders line and content items.
/// </summary>
public enum LinePosition
{
    /// <summary>
    /// The CrestTimeline line is displayed at the center of the component.
    /// </summary>
    Center,

    /// <summary>
    /// The CrestTimeline line is displayed at the center of the component with alternating content position.
    /// </summary>
    Alternate,

    /// <summary>
    /// The CrestTimeline line is displayed at the start of the component.
    /// </summary>
    Start,

    /// <summary>
    /// The CrestTimeline line is displayed at the end of the component.
    /// </summary>
    End,

    /// <summary>
    /// The CrestTimeline line is displayed at the left side of the component.
    /// </summary>
    Left,

    /// <summary>
    /// The CrestTimeline line is displayed at the right side of the component.
    /// </summary>
    Right,

    /// <summary>
    /// The CrestTimeline line is displayed at the top of the component.
    /// </summary>
    Top,

    /// <summary>
    /// The CrestTimeline line is displayed at the bottom of the component.
    /// </summary>
    Bottom
}

