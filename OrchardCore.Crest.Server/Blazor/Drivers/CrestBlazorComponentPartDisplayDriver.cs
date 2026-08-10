using Crest.Blazor.Models;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.DisplayManagement.Views;

namespace Crest.Blazor.Drivers;

// Produces a shape whose ShapeMetadata.Type is the tenant-chosen ComponentName rather than
// a fixed "CrestBlazorComponentPart" type - CrestBlazorComponentShapeBindingResolver is
// what actually turns that shape type into rendered HTML (see its own comment). This
// driver's only job is the hand-off: pick the right shape type, carry Parameters through.
// No settings needed for now - matches Templates' own DisplayDriver-does-nothing-fancy
// shape for the analogous hand-off (TemplatesShapeBindingResolver has no matching display
// driver at all, since it overrides *existing* shape types rather than a dedicated part).
public sealed class CrestBlazorComponentPartDisplayDriver : ContentPartDisplayDriver<CrestBlazorComponentPart>
{
    public override IDisplayResult Display(CrestBlazorComponentPart part, BuildPartDisplayContext context)
    {
        if (string.IsNullOrEmpty(part.ComponentName))
        {
            return null;
        }

        return Initialize<CrestBlazorComponentShapeViewModel>(part.ComponentName, model =>
        {
            model.Parameters = part.Parameters;

            // CrestBlazorComponentShapeBindingResolver reads component parameters from
            // IShape.Properties (the same storage Liquid's shape_new/shape_render path
            // uses - see the resolver's own comment), not from this model's own Parameters
            // CLR property, which Composite's dynamic-property machinery never sees. Copy
            // explicitly so both shape-creation paths converge on one read mechanism.
            foreach (var (key, value) in part.Parameters)
            {
                model.Properties[key] = value;
            }
        }).Location("Detail", "Content");
    }
}
