namespace Crest.Components.Primitives;

/// <summary>
/// Represents a cell in <see cref="CrestTable"/>
/// </summary>
public partial class CrestTableCell : CrestComponentWithChildren
{
    /// <inheritdoc />
    protected override string GetComponentCssClass() => "rz-data-cell";
}