namespace Crest.Components.Primitives;

/// <summary>
/// Specifies the ways a <see cref="Crest.Components.Primitives.CrestTabs" /> component renders its items.
/// </summary>
public enum TabRenderMode
{
    /// <summary>
    /// The CrestTabs component switches its items server side. Only the selected item is rendered.
    /// </summary>
    Server,

    /// <summary>
    /// The CrestTabs components switches its items client-side. All items are rendered and the unselected ones are hidden with CSS.
    /// </summary>
    Client
}

