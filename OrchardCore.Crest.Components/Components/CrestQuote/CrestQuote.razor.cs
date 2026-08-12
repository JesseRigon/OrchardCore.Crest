using Crest.Components.Blazor;
using Microsoft.AspNetCore.Components;

namespace Crest.Components.Primitives;

/// <summary>
/// CrestQuote component - renders a single block quote. First real,
/// content-driven consumer of CrestBlazorComponentPart's rendering path.
/// </summary>
[CrestBlazorComponent("CrestQuote")]
public partial class CrestQuote : ComponentBase
{
    [Parameter]
    public string? Text { get; set; }
}
