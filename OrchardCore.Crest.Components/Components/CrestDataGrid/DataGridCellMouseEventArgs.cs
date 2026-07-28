using Crest.Components.Primitives;

namespace Crest.Components.Primitives;

/// <summary>
/// Supplies information about a <see cref="CrestDataGrid{TItem}.CellContextMenu" /> event that is being raised.
/// </summary>
/// <typeparam name="T">The data item type.</typeparam>
public class DataGridCellMouseEventArgs<T> : Microsoft.AspNetCore.Components.Web.MouseEventArgs where T : notnull
{
    /// <summary>
    /// Gets the data item which the clicked DataGrid row represents.
    /// </summary>
    public T? Data { get; internal set; }

    /// <summary>
    /// Gets the CrestDataGridColumn which this cells represents.
    /// </summary>
    public CrestDataGridColumn<T>? Column { get; internal set; }
}

