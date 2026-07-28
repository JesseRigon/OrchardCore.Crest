using Crest.Components.Primitives;

namespace Crest.Components.Primitives;

/// <summary>
/// Supplies information about a <see cref="CrestDataGrid{TItem}.ColumnReordered" /> event that is being raised.
/// </summary>
/// <typeparam name="T">The data item type.</typeparam>
public class DataGridColumnReorderedEventArgs<T> where T : notnull
{
    /// <summary>
    /// Gets the reordered CrestDataGridColumn.
    /// </summary>
    public CrestDataGridColumn<T>? Column { get; internal set; }

    /// <summary>
    /// Gets the old index of the column.
    /// </summary>
    public int OldIndex { get; internal set; }

    /// <summary>
    /// Gets the new index of the column.
    /// </summary>
    public int NewIndex { get; internal set; }
}

