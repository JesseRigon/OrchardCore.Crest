namespace Crest.Components.Primitives
{
    /// <summary>
    /// Specifies the interpolation mode of lines between data points. Used by <see cref="CrestAreaSeries{TItem}"/> and <see cref="CrestLineSeries{TItem}"/>.
    /// </summary>
    public enum Interpolation
    {

        /// <summary>
        /// Points are connected by a straight line.
        /// </summary>
        Line,
        /// <summary>
        /// Points are connected by a smooth curve.
        /// </summary>
        Spline,
        /// <summary>
        /// Points are connected by horizontal and vertical lines only.
        /// </summary>
        Step
    }
}
