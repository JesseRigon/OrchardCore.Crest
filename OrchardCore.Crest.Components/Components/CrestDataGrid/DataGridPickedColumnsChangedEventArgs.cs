using System.Collections.Generic;
using Crest.Components.Primitives;

namespace Crest.Components.Primitives;

/// <summary>
/// Supplies information about a <see cref="CrestDataGrid{TItem}.PickedColumnsChanged" /> event that is being raised.
/// </summary>
/// <typeparam name="T">The data item type.</typeparam>
public class DataGridPickedColumnsChangedEventArgs<T> where T : notnull
{
    /// <summary>
    /// Gets the picked columns.
    /// </summary>
    public IEnumerable<CrestDataGridColumn<T>>? Columns { get; internal set; }
}

