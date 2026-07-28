using System.Collections.Generic;

namespace Crest.Components.Primitives
{
    /// <summary>
    /// Marker interface for <see cref="CrestStackedColumnSeries{TItem}" />.
    /// </summary>
    public interface IChartStackedColumnSeries
    {
        /// <summary>
        /// Gets the count.
        /// </summary>
        /// <value>The count.</value>
        int Count { get; }

        /// <summary>
        /// Gets the values for category.
        /// </summary>
        IEnumerable<double> ValuesForCategory(double category);

        /// <summary>
        /// Gets the items for category.
        /// </summary>
        /// <param name="category"></param>
        /// <returns></returns>
        IEnumerable<object> ItemsForCategory(double category);

        /// <summary>
        /// Gets the value at the specified index.
        /// </summary>
        double ValueAt(int index);
    }
}