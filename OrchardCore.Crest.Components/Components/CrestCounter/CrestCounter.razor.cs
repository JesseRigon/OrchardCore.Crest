using Microsoft.AspNetCore.Components;

namespace Crest.Components.Primitives;

/// <summary>
/// CrestCounter component - a minimal interactive island (see
/// plans/blazor hybrid conversion.md, Phase 3.5 / interactive-island note).
/// Needs an actual interactive render mode (InteractiveServer/
/// InteractiveWebAssembly/InteractiveAuto) to be clickable - rendered through
/// CrestBlazorComponentShapeBindingResolver's Static-SSR-only HtmlRenderer path,
/// the button renders but is inert (no circuit, no WASM runtime attached).
/// </summary>
public partial class CrestCounter : ComponentBase
{
    [Parameter]
    public int StartValue { get; set; }

    private int Count { get; set; }

    protected override void OnParametersSet()
    {
        Count = StartValue;
    }

    private void Increment()
    {
        Count++;
    }
}
