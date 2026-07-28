namespace Crest.Components.Primitives
{
    /// <summary>
    /// Common axis API of <see cref="CrestChart" />
    /// </summary>
    public interface IChartAxis
    {
        /// <summary>
        /// Gets or sets the grid lines configuration of this axis.
        /// </summary>
        CrestGridLines GridLines { get; set; }

        /// <summary>
        /// Gets or sets the crosshair configuration of this axis.
        /// </summary>
        CrestAxisCrosshair Crosshair { get; set; }
    }
}