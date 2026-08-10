using OrchardCore.DisplayManagement.Shapes;

namespace Crest.Blazor.Models;

// The shape value CrestBlazorComponentShapeBindingResolver reads Parameters from. Kept
// deliberately minimal - Parameters is the same string-keyed bag CrestBlazorComponentPart
// stores, just carried through to the shape so the resolver doesn't need to re-query the
// content item.
//
// Inherits Shape (not a bare POCO): ShapeFactoryExtensions.CreateStronglyTypedShape (called
// by DisplayDriverBase.Initialize<TModel>) only Castle-proxies a model type when it does
// NOT already implement IShape - types that already implement IShape are instantiated
// directly via Activator.CreateInstance, no proxy needed. Inheriting Shape sidesteps the
// proxy machinery entirely (no sealed/virtual constraints to worry about) and is a
// documented, supported path for shape models.
public sealed class CrestBlazorComponentShapeViewModel : Shape
{
    public IReadOnlyDictionary<string, string> Parameters { get; set; } = new Dictionary<string, string>();
}
