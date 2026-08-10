using Crest.Blazor.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Descriptors;
using OrchardCore.DisplayManagement.Implementation;

namespace Crest.Blazor;

// Participates in Orchard's own shape pipeline (DefaultHtmlDisplay.GetShapeBindingAsync
// tries every registered IShapeBindingResolver before the compiled ShapeTable) exactly
// the way OrchardCore.Templates' TemplatesShapeBindingResolver overrides a shape's
// rendering with tenant-stored Liquid - here the override renders a Blazor component with
// .NET's own HtmlRenderer instead. Falls through (returns null) for every shape that isn't
// a registered component name, so normal Liquid/Razor/Templates-resolved shapes are
// unaffected. See plans/blazor hybrid conversion.md, Phase 3b.
//
// Static SSR only: the component renders once, inline, to static HTML - no hydration, no
// @rendermode. Genuinely interactive "islands" are a separate concern (Phase 2's whole-page
// hosting/WASM client glob), not this resolver's job.
public sealed class CrestBlazorComponentShapeBindingResolver : IShapeBindingResolver
{
    private readonly ICrestBlazorComponentRegistry _registry;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILoggerFactory _loggerFactory;

    public CrestBlazorComponentShapeBindingResolver(
        ICrestBlazorComponentRegistry registry,
        IServiceProvider serviceProvider,
        ILoggerFactory loggerFactory)
    {
        _registry = registry;
        _serviceProvider = serviceProvider;
        _loggerFactory = loggerFactory;
    }

    public Task<ShapeBinding> GetShapeBindingAsync(string shapeType)
    {
        if (!_registry.TryResolve(shapeType, out var componentType))
        {
            return Task.FromResult<ShapeBinding>(null);
        }

        return Task.FromResult(BuildShapeBinding(shapeType, componentType));
    }

    private ShapeBinding BuildShapeBinding(string shapeType, Type componentType)
    {
        return new ShapeBinding
        {
            BindingName = shapeType,
            BindingSource = $"Blazor/{shapeType}",
            BindingAsync = async displayContext =>
            {
                var parameters = ParametersFromShape(displayContext.Value);

                await using var renderer = new HtmlRenderer(_serviceProvider, _loggerFactory);

                var html = await renderer.Dispatcher.InvokeAsync(async () =>
                {
                    var root = await renderer.RenderComponentAsync(componentType, parameters);
                    return root.ToHtmlString();
                });

                return new Microsoft.AspNetCore.Html.HtmlString(html);
            },
        };
    }

    // Two different shape-creation paths reach this resolver, and both need to work:
    //  1. CrestBlazorComponentPartDisplayDriver's Initialize<CrestBlazorComponentShapeViewModel>
    //     (a tenant-placed CrestBlazorComponentPart) - ShapeFactory.CreateAsync<TModel> builds
    //     a dynamic proxy castable to TModel itself, but CrestBlazorComponentShapeViewModel
    //     inherits Shape, so its Parameters dictionary also ends up as ordinary shape
    //     properties once set - same underlying storage as path 2.
    //  2. Liquid's {{ "ComponentName" | shape_new: text: "..." | shape_render }}
    //     (NewShapeFilter) - IShapeFactory.CreateAsync(shapeType, Arguments.From(properties)),
    //     which is untyped and stores each named argument directly in IShape.Properties.
    // IShape.Properties is the common ground for both: reading it directly (rather than
    // casting to CrestBlazorComponentShapeViewModel) also means an IShape.Properties key
    // maps onto a component's [Parameter] by name exactly the way Liquid/Razor shape
    // templates already read @Model.X - the natural, general mapping for Blazor's own
    // [Parameter] mechanism, not something specific to CrestBlazorComponentPart.
    private static ParameterView ParametersFromShape(IShape shape)
    {
        if (shape is null || shape.Properties.Count == 0)
        {
            return ParameterView.Empty;
        }

        return ParameterView.FromDictionary(new Dictionary<string, object?>(shape.Properties));
    }
}
