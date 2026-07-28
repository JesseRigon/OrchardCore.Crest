using Microsoft.AspNetCore.Components;

namespace Crest.Components.Primitives
{
    /// <summary>
    /// Renders donut series in <see cref="CrestChart" />.
    /// </summary>
    /// <typeparam name="TItem">The type of the series data item.</typeparam>
    public partial class CrestDonutSeries<TItem> : CrestPieSeries<TItem>, IChartDonutSeries
    {
        /// <summary>
        /// Gets or sets the inner radius of the donut.
        /// </summary>
        /// <value>The inner radius.</value>
        [Parameter]
        public double? InnerRadius { get; set; }

        /// <summary>
        /// Gets or sets the title template.
        /// </summary>
        /// <value>The title template.</value>
        [Parameter]
        public RenderFragment? TitleTemplate { get; set; }

        /// <inheritdoc />
        internal override double LabelInnerRadius(double outerRadius)
        {
            return InnerRadius ?? outerRadius / 2;
        }
    }
}
