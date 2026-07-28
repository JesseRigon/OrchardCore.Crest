namespace Crest.Components.Primitives;

/// <summary>
/// Supplies information about a <see cref="Crest.Components.Primitives.CrestTree.ItemContextMenu" /> event that is being raised.
/// </summary>
public class TreeItemContextMenuEventArgs : Microsoft.AspNetCore.Components.Web.MouseEventArgs
{
    /// <summary>
    /// Gets the tree item text.
    /// </summary>
    public string? Text { get; internal set; }

    /// <summary>
    /// Gets the tree item value.
    /// </summary>
    public object? Value { get; internal set; }
}

