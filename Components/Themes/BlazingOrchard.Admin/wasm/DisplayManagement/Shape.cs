using Microsoft.AspNetCore.Components;

namespace BlazingOrchard.Admin.DisplayManagement;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class ShapeAttribute(string shapeType) : Attribute
{
    public string ShapeType { get; } = shapeType;
}

public sealed class Shape(string type)
{
    public ShapeMetadata Metadata { get; } = new(type);
    public Dictionary<string, object?> Properties { get; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class ShapeMetadata(string type)
{
    public string Type { get; } = type;
    public IList<string> Alternates { get; } = [];
}

public class ShapeTemplate : ComponentBase
{
    [Parameter]
    public Shape Model { get; set; } = default!;
}
