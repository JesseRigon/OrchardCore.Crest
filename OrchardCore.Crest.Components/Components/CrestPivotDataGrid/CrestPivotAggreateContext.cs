using System.Linq;
using Crest.Components.Primitives;

namespace Crest.Components.Primitives;

/// <summary>
/// CrestPivotAggreateContext.
/// </summary>
public class CrestPivotAggreateContext<T>
{
    /// <summary>
    /// Gets the query.
    /// </summary>
    public IQueryable<T>? View { get; internal set; }

    /// <summary>
    /// Gets the aggregate.
    /// </summary>
    public CrestPivotAggregate<T>? Aggregate { get; internal set; }

    /// <summary>
    /// Gets the aggregate value.
    /// </summary>
    public object? Value { get; internal set; }

    /// <summary>
    /// Gets the row index.
    /// </summary>
    public int? Index { get; internal set; }
}

