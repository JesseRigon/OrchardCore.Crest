using Microsoft.AspNetCore.Components;
using Crest.Components.Primitives;

namespace Crest.Components.Primitives;

/// <summary>
/// Supplies information about a <see cref="CrestTree.Expand" /> event that is being raised.
/// </summary>
public class TreeExpandEventArgs
{
    /// <summary>
    /// Gets the <see cref="Crest.Components.Primitives.CrestTreeItem.Value" /> the expanded CrestTreeItem.
    /// </summary>
    public object? Value { get; set; }

    /// <summary>
    /// Gets the <see cref="Crest.Components.Primitives.CrestTreeItem.Text" /> the expanded CrestTreeItem.
    /// </summary>
    public string? Text { get; set; }

    /// <summary>
    /// Gets or sets the children of the expanded CrestTreeItem.
    /// </summary>
    public TreeItemSettings? Children { get; set; }
}

