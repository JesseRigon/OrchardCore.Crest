using System.Collections.Generic;

namespace Crest.Components.Primitives;

/// <summary>
/// Supplies information about a <see cref="Crest.Components.Primitives.CrestDataGrid{TItem}.LoadChildData" /> event that is being raised.
/// </summary>
/// <typeparam name="T">The data item type.</typeparam>
public class DataGridLoadChildDataEventArgs<T>
{
    /// <summary>
    /// Gets or sets the data.
    /// </summary>
    /// <value>The data.</value>
    public IEnumerable<T>? Data { get; set; }

    /// <summary>
    /// Gets the item.
    /// </summary>
    /// <value>The item.</value>
    public T? Item { get; internal set; }
}

