using System.Collections.Generic;

namespace Crest.Components.Primitives
{
    /// <summary>
    /// Marker interface for <see cref="CrestStackedBarSeries{TItem}" />.
    /// </summary>
    public interface IChartStackedBarSeries : IChartBarSeries
    {
        /// <summary>
        /// Gets the value at the specified index.
        /// </summary>
        double ValueAt(int index);

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
    }
}