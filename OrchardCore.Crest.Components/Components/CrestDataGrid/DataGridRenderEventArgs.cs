using Crest.Components.Primitives;

namespace Crest.Components.Primitives;

/// <summary>
/// Supplies information about a <see cref="CrestDataGrid{TItem}.Render" /> event that is being raised.
/// </summary>
/// <typeparam name="T">The data item type.</typeparam>
public class DataGridRenderEventArgs<T> where T : notnull
{
    /// <summary>
    /// Gets the instance of the CrestDataGrid component which has rendered.
    /// </summary>
    public CrestDataGrid<T>? Grid { get; internal set; }

    /// <summary>
    /// Gets a value indicating whether this is the first time the CrestDataGrid has rendered.
    /// </summary>
    /// <value><c>true</c> if this is the first time; otherwise, <c>false</c>.</value>
    public bool FirstRender { get; internal set; }
}

