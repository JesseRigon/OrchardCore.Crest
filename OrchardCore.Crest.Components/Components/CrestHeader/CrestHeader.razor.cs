using Microsoft.AspNetCore.Components;
using Crest.Components.Primitives.Rendering;

namespace Crest.Components.Primitives
{
    /// <summary>
    /// CrestHeader component.
    /// </summary>
    public partial class CrestHeader : CrestComponentWithChildren
    {
        /// <summary>
        /// The <see cref="CrestLayout" /> this component is nested in.
        /// </summary>
        [CascadingParameter]
        public CrestLayout? Layout { get; set; }

        /// <inheritdoc />
        protected override string GetComponentCssClass()
        {
            return ClassList.Create("rz-header")
                            .Add("fixed", Layout == null)
                            .ToString();
        }
    }
}
