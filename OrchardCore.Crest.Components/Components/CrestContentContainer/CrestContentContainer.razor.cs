using Microsoft.AspNetCore.Components;

namespace Crest.Components.Primitives
{
    /// <summary>
    /// CrestContentContainer component.
    /// </summary>
    public partial class CrestContentContainer : CrestComponentWithChildren
    {
        /// <summary>
        /// Gets or sets the name.
        /// </summary>
        /// <value>The name.</value>
        [Parameter]
        public string? Name { get; set; }
    }
}
