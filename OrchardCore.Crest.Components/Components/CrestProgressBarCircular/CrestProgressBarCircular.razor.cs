using Microsoft.AspNetCore.Components;
using Crest.Components.Primitives.Rendering;

namespace Crest.Components.Primitives
{
    /// <summary>
    /// A circular progress indicator component for showing task completion or ongoing processes in a compact circular format.
    /// CrestProgressBarCircular displays progress as a ring with determinate (specific value) or indeterminate (spinning) modes.
    /// Space-efficient and works well for dashboards, loading states, or anywhere circular design fits better than linear bars.
    /// Inherits all features from CrestProgressBar and adds circular design showing progress as a ring/arc instead of a linear bar,
    /// size options (ExtraSmall, Small, Medium, Large) for different contexts, value display in the center of the circle, and compact design taking less space than linear progress bars.
    /// Use for dashboard KPIs, button loading states, or compact loading indicators. The circular shape makes it ideal for displaying progress where space is limited.
    /// </summary>
    /// <example>
    /// Basic circular progress:
    /// <code>
    /// &lt;CrestProgressBarCircular Value=@completionPercentage ShowValue="true" /&gt;
    /// </code>
    /// Small loading spinner:
    /// <code>
    /// &lt;CrestProgressBarCircular Mode="ProgressBarMode.Indeterminate" 
    ///                             Size="ProgressBarCircularSize.Small" 
    ///                             ProgressBarStyle="ProgressBarStyle.Primary" /&gt;
    /// </code>
    /// Dashboard metric with custom range:
    /// <code>
    /// &lt;CrestProgressBarCircular Value=@salesCount Max="1000" Size="ProgressBarCircularSize.Large" 
    ///                             Unit=" sales" ShowValue="true" ProgressBarStyle="ProgressBarStyle.Success" /&gt;
    /// </code>
    /// </example>
    public partial class CrestProgressBarCircular : CrestProgressBar
    {
        /// <inheritdoc />
        protected override string GetComponentCssClass() => ClassList.Create("rz-progressbar-circular")
                                                                     .Add("rz-progressbar-determinate", Mode == ProgressBarMode.Determinate)
                                                                     .Add("rz-progressbar-indeterminate", Mode == ProgressBarMode.Indeterminate)
                                                                     .Add($"rz-progressbar-{ProgressBarStyle.ToString().ToLowerInvariant()}")
                                                                     .Add($"rz-progressbar-circular-{CircleSize}")
                                                                     .ToString();

        string CircleSize => Size switch
        {
            ProgressBarCircularSize.Medium => "md",
            ProgressBarCircularSize.Large => "lg",
            ProgressBarCircularSize.Small => "sm",
            ProgressBarCircularSize.ExtraSmall => "xs",
            _ => string.Empty,
        };

        /// <summary>
        /// Gets or sets the size of the circular progress indicator.
        /// Controls the diameter of the circle: ExtraSmall, Small, Medium, or Large.
        /// </summary>
        /// <value>The circular progress bar size. Default is <see cref="ProgressBarCircularSize.Medium"/>.</value>
        [Parameter]
        public ProgressBarCircularSize Size { get; set; } = ProgressBarCircularSize.Medium;
    }
} 