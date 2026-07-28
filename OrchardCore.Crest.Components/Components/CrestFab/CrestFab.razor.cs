using Crest.Components.Primitives.Rendering;

namespace Crest.Components.Primitives;

/// <summary>
/// CrestFab component.
/// </summary>
/// <example>
/// <code>
/// &lt;CrestFab Icon="add" Click=@(args => Console.WriteLine("FAB clicked")) /&gt;
/// &lt;CrestFab Icon="add" IsBusy="@isLoading" BusyText="Loading..." Click=@OnFabClick /&gt;
/// </code>
/// </example>
public partial class CrestFab : CrestButton
{
    /// <inheritdoc />
    public override ButtonSize Size { get; set; } = ButtonSize.Large;

    /// <inheritdoc />
    protected override string GetComponentCssClass()
    {
        return ClassList.Create("rz-button rz-fab")
                       .AddButtonSize(Size)
                       .AddVariant(Variant)
                       .AddButtonStyle(ButtonStyle)
                       .AddDisabled(IsDisabled)
                       .AddShade(Shade)
                       .Add($"rz-button-icon-only", string.IsNullOrEmpty(Text) && !string.IsNullOrEmpty(Icon))
                       .ToString();
    }
}