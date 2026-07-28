namespace Crest.Components.Primitives;

/// <summary>
/// Specifies the ways a <see cref="Crest.Components.Primitives.CrestAccordion" /> component renders its items.
/// </summary>
public enum AccordionRenderMode
{
    /// <summary>
    /// The CrestAccordion component switches its items server side. The component re-renders on every expand/collapse.
    /// </summary>
    Server,

    /// <summary>
    /// The CrestAccordion component switches its items client-side. All items are rendered and the expand/collapse is handled with JavaScript.
    /// </summary>
    Client
}
