namespace Crest.Admin.Components.Admin;

/// <summary>
/// Small Blazor-side drag/drop payload holder, following Elsa Studio's CascadingValue pattern.
/// Browser drag events carry the DOM interaction; Blazor keeps the typed payload here.
/// </summary>
public sealed class DragDropManager
{
    public object? Payload { get; set; }
}
