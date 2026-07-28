namespace Crest.Components.Primitives;

/// <summary>
/// Supplies information about a <see cref="Crest.Components.Primitives.CrestTree.Change" /> event that is being raised.
/// </summary>
public class TreeEventArgs
{
    /// <summary>
    /// Gets the <see cref="Crest.Components.Primitives.CrestTreeItem.Text" /> the selected CrestTreeItem.
    /// </summary>
    public string? Text { get; set; }

    /// <summary>
    /// Gets the <see cref="Crest.Components.Primitives.CrestTreeItem.Value" /> the selected CrestTreeItem.
    /// </summary>
    public object? Value { get; set; }
}

