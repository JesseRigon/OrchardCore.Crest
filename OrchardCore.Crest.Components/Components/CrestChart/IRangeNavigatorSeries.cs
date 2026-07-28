using Microsoft.AspNetCore.Components;

namespace Crest.Components.Primitives
{
    /// <summary>
    /// Represents a series that can be rendered inside a <see cref="CrestRangeNavigator" />.
    /// </summary>
    public interface IRangeNavigatorSeries
    {
        /// <summary>
        /// Transforms the category scale based on the series data.
        /// </summary>
        ScaleBase TransformCategoryScale(ScaleBase scale);

        /// <summary>
        /// Transforms the value scale based on the series data.
        /// </summary>
        ScaleBase TransformValueScale(ScaleBase scale);

        /// <summary>
        /// Renders the series using the specified scales.
        /// </summary>
        RenderFragment Render(ScaleBase categoryScale, ScaleBase valueScale);
    }
}
