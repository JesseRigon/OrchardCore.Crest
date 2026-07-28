using Crest.Components.Primitives;

namespace Crest.Components.Primitives;

/// <summary>
/// Supplies information about a <see cref="CrestDataGrid{TItem}.ColumnResized" /> event that is being raised.
/// </summary>
/// <typeparam name="T">The data item type.</typeparam>
public class DataGridColumnResizedEventArgs<T> where T : notnull
{
    /// <summary>
    /// Gets the resized CrestDataGridColumn.
    /// </summary>
    public CrestDataGridColumn<T>? Column { get; internal set; }

    /// <summary>
    /// Gets the new width of the resized column.
    /// </summary>
    public double Width { get; internal set; }
}

