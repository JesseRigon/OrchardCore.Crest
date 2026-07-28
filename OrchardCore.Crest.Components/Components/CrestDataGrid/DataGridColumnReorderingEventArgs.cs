using Crest.Components.Primitives;

namespace Crest.Components.Primitives;

/// <summary>
/// Supplies information about a <see cref="CrestDataGrid{TItem}.ColumnReordering" /> event that is being raised.
/// </summary>
/// <typeparam name="T">The data item type.</typeparam>
public class DataGridColumnReorderingEventArgs<T> where T : notnull
{
    /// <summary>
    /// Gets the reordered CrestDataGridColumn.
    /// </summary>
    public CrestDataGridColumn<T>? Column { get; internal set; }

    /// <summary>
    /// Gets the reordered to CrestDataGridColumn.
    /// </summary>
    public CrestDataGridColumn<T>? ToColumn { get; internal set; }

    /// <summary>
    /// Gets or sets a value which will cancel the event.
    /// </summary>
    /// <value><c>true</c> to cancel the event; otherwise, <c>false</c>.</value>
    public bool Cancel { get; set; }
}

