using Microsoft.AspNetCore.Components;
using Crest.Components.Primitives.Rendering;

namespace Crest.Components.Primitives
{
    /// <summary>
    /// CrestFooter component.
    /// </summary>
    public partial class CrestFooter : CrestComponentWithChildren
    {
        /// <summary>
        /// The <see cref="CrestLayout" /> this component is nested in.
        /// </summary>
        [CascadingParameter]
        public CrestLayout? Layout { get; set; }

        /// <inheritdoc />
        protected override string GetComponentCssClass()
        {
            return ClassList.Create("rz-footer")
                            .Add("fixed", Layout == null)
                            .ToString();
        }
    }
}
