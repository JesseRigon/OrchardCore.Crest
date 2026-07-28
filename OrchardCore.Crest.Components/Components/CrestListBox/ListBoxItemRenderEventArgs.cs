using Crest.Components.Primitives;

namespace Crest.Components.Primitives;

/// <summary>
/// Supplies information about CrestListBox ItemRender event.
/// </summary>
public class ListBoxItemRenderEventArgs<TValue> : DropDownBaseItemRenderEventArgs<TValue>
{
    /// <summary>
    /// Gets the ListBox.
    /// </summary>
    public CrestListBox<TValue>? ListBox { get; internal set; }
}

