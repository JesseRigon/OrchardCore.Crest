using System.Threading.Tasks;

namespace Crest.Components.Primitives
{
    /// <summary>
    /// Non-generic contract for <see cref="CrestSpiderChart"/> used by configuration components
    /// like <see cref="CrestSpiderLegend"/> without relying on reflection (important for trimming/AOT).
    /// </summary>
    public interface ICrestSpiderChart
    {
        /// <summary>
        /// Gets or sets the legend configuration for the chart.
        /// </summary>
        CrestSpiderLegend Legend { get; set; }

        /// <summary>
        /// Requests the chart to refresh its rendering.
        /// </summary>
        Task Refresh();
    }
}

