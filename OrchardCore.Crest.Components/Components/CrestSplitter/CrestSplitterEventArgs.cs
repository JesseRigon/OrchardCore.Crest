using Crest.Components.Primitives;

namespace Crest.Components.Primitives;

/// <summary>
/// Supplies information about a <see cref="CrestSplitter.Expand" /> or <see cref="CrestSplitter.Collapse" /> event that is being raised.
/// </summary>
public class CrestSplitterEventArgs
{
    /// <summary>
    /// Gets the index of the pane.
    /// </summary>
    public int PaneIndex { get; set; }

    /// <summary>
    /// Gets the pane which the event applies to.
    /// </summary>
    /// <value>The pane.</value>
    public CrestSplitterPane? Pane { get; set; }

    /// <summary>
    /// Gets or sets a value which will cancel the event.
    /// </summary>
    /// <value><c>true</c> to cancel the event; otherwise, <c>false</c>.</value>
    public bool Cancel { get; set; }
}

