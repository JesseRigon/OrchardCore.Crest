using Crest.Components.Primitives;

namespace Crest.Components.Primitives;

/// <summary>
/// Supplies information about a <see cref="CrestDataGrid{TItem}.Sort" /> event that is being raised.
/// </summary>
/// <typeparam name="T">The data item type.</typeparam>
public class DataGridColumnSortEventArgs<T> where T : notnull
{
    /// <summary>
    /// Gets the sorted CrestDataGridColumn.
    /// </summary>
    public CrestDataGridColumn<T>? Column { get; internal set; }

    /// <summary>
    /// Gets the new sort order of the sorted column.
    /// </summary>
    public SortOrder? SortOrder { get; internal set; }
}

