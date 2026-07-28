using Microsoft.AspNetCore.Components;
using Crest.Components.Primitives;

namespace Crest.Components.Primitives;

/// <summary>
/// A base class of components that have child content.
/// </summary>
public class CrestComponentWithChildren : CrestComponent
{
    /// <summary>
    /// Gets or sets the child content
    /// </summary>
    /// <value>The content of the child.</value>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }
}

