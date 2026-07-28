namespace Crest.Components.Primitives;

/// <summary>
/// Supplies information about a <see cref="Crest.Components.Primitives.CrestSplitter.Resize" /> event that is being raised.
/// </summary>
public class CrestSplitterResizeEventArgs : CrestSplitterEventArgs
{
    /// <summary>
    /// The new size of the pane
    /// </summary>
    public double NewSize { get; set; }
}

