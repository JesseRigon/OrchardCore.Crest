using Microsoft.AspNetCore.Components;

namespace Crest.Components.Primitives
{
    /// <summary>
    /// CrestContent component.
    /// </summary>
    public partial class CrestContent : CrestComponentWithChildren
    {
        /// <summary>
        /// Gets or sets the container.
        /// </summary>
        /// <value>The container.</value>
        [Parameter]
        public string? Container { get; set; }

        /// <inheritdoc />
        protected override string GetComponentCssClass()
        {
            return "content";
        }
    }
}
