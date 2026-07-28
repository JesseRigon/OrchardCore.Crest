namespace Crest.Components.Primitives
{
    /// <summary>
    /// Specifies when <see cref="CrestChart" /> displays tooltips. Used by <see cref="CrestChartTooltipOptions" />.
    /// </summary>
    public enum ChartTooltipTrigger
    {
        /// <summary>
        /// Uses <see cref="Axis" /> when the category axis crosshair is enabled or the chart belongs to a <see cref="CrestChart.SyncGroup" />; otherwise <see cref="Point" />.
        /// </summary>
        Auto,
        /// <summary>
        /// The tooltip is displayed when the cursor is near a data point (within <see cref="CrestChart.TooltipTolerance" />).
        /// </summary>
        Point,
        /// <summary>
        /// The tooltip follows the nearest category - it is displayed for the data point closest to the cursor by horizontal distance,
        /// anywhere inside the plot area. Matches the behavior of crosshairs and synchronized charts.
        /// </summary>
        Axis
    }
}
