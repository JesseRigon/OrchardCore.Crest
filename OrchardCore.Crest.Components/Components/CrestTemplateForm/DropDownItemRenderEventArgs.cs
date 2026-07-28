using Crest.Components.Primitives;

namespace Crest.Components.Primitives;

/// <summary>
/// Supplies information about CrestDropDown ItemRender event.
/// </summary>
public class DropDownItemRenderEventArgs<TValue> : DropDownBaseItemRenderEventArgs<TValue>
{
    /// <summary>
    /// Gets the DropDown.
    /// </summary>
    public CrestDropDown<TValue>? DropDown { get; internal set; }
}
