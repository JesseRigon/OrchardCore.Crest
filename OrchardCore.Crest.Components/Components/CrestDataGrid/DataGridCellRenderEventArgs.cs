using Crest.Components.Primitives;

namespace Crest.Components.Primitives;

/// <summary>
/// Supplies information about a <see cref="CrestDataGrid{TItem}.CellRender" /> event that is being raised.
/// </summary>
/// <typeparam name="T">The data item type.</typeparam>
public class DataGridCellRenderEventArgs<T> : RowRenderEventArgs<T> where T : notnull
{
    /// <summary>
    /// Gets the CrestDataGridColumn which this cells represents.
    /// </summary>
    public CrestDataGridColumn<T>? Column { get; internal set; }
}

