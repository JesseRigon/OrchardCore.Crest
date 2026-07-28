using Microsoft.AspNetCore.Components;

namespace Crest.Components.Primitives
{
    /// <summary>
    /// Marker interface for <see cref="CrestColumnSeries{TItem}" />.
    /// </summary>
    public interface IChartDonutSeries
    {
        /// <summary>
        /// Renders the title.
        /// </summary>
        /// <param name="x">The x.</param>
        /// <param name="y">The y.</param>
        /// <returns>RenderFragment.</returns>
        RenderFragment RenderTitle(double x, double y);
    }
}