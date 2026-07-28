using Microsoft.AspNetCore.Components;

namespace Crest.Components.Primitives;

/// <summary>
/// CrestFabMenuItem component.
/// </summary>
/// <example>
/// <code>
/// &lt;CrestFabMenuItem Text="Folder" Icon="folder" Click=@(args => Console.WriteLine("Item clicked")) /&gt;
/// </code>
/// </example>
public partial class CrestFabMenuItem : CrestButton
{
    /// <inheritdoc />
    [Parameter]
    public override Variant Variant { get; set; } = Variant.Flat;

    /// <inheritdoc />
    [Parameter]
    public override Shade Shade { get; set; } = Shade.Light;

    /// <inheritdoc />
    [Parameter]
    public override ButtonSize Size { get; set; } = ButtonSize.Large;
}