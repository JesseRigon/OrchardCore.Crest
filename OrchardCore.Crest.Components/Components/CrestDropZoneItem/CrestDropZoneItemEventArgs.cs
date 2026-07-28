using Microsoft.AspNetCore.Components.Web;
using Crest.Components.Primitives;

namespace Crest.Components.Primitives;

/// <summary>
/// Supplies information about CrestDropZoneContainer CanDrop function and CrestDropZone Drop event.
/// </summary>
public class CrestDropZoneItemEventArgs<TItem>
{
    /// <summary>
    /// Gets the dragged item zone.
    /// </summary>
    public CrestDropZone<TItem>? FromZone { get; internal set; }

    /// <summary>
    /// Gets the drop zone.
    /// </summary>
    public CrestDropZone<TItem>? ToZone { get; internal set; }

    /// <summary>
    /// Gets the dragged item.
    /// </summary>
    public TItem? Item { get; internal set; }

    /// <summary>
    /// Gets the dropped item.
    /// </summary>
    public TItem? ToItem { get; internal set; }

    /// <summary>
    /// The data that underlies a drag-and-drop operation, known as the drag data store.
    /// See <see cref="DataTransfer"/>.
    /// </summary>
    public DataTransfer DataTransfer { get; set; } = default!;
}

