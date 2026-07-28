using Crest.Components.Primitives;

namespace Crest.Components.Primitives;

/// <summary>
/// Supplies information about a <see cref="CrestDataGrid{TItem}.Group" /> event that is being raised.
/// </summary>
/// <typeparam name="T">The data item type.</typeparam>
public class DataGridColumnGroupEventArgs<T> where T : notnull
{
    /// <summary>
    /// Gets the grouped CrestDataGridColumn.
    /// </summary>
    public CrestDataGridColumn<T>? Column { get; internal set; }

    /// <summary>
    /// Gets the group descriptor.
    /// </summary>
    public GroupDescriptor? GroupDescriptor { get; internal set; }
}

