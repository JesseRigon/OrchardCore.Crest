using Crest.Blazor;
using Crest.Blazor.Models;
using Crest.Components.Primitives;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using NSubstitute;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Descriptors;
using OrchardCore.DisplayManagement.Implementation;
using OrchardCore.DisplayManagement.Theming;
using Xunit;

namespace Crest.Server.Tests;

// Goes one level deeper than CrestBlazorComponentShapeBindingResolverTests: instead of
// hand-building the CrestBlazorComponentShapeViewModel and calling the resolver directly,
// this drives the real chain a live request would use -
// IShapeFactory.CreateAsync<TModel>(shapeType, initialize) (Orchard's real dynamic-proxy/
// Shape machinery, the same DefaultShapeFactory a live tenant uses, and the same call
// DisplayDriverBase.Initialize<TModel> makes internally on CrestBlazorComponentPart's
// behalf) -> IHtmlDisplay.ExecuteAsync with CrestBlazorComponentShapeBindingResolver
// registered as one of N IShapeBindingResolvers, discovered by GetShapeBindingAsync's real
// resolver-enumeration loop (not called directly) -> real HtmlRenderer output.
// IShapeTableManager/IThemeManager are faked to an empty ShapeTable, since resolvers are
// checked before the table is ever consulted (DefaultHtmlDisplay.GetShapeBindingAsync) -
// nothing else in the display pipeline is faked.
public sealed class CrestBlazorComponentPipelineIntegrationTests
{
    [Fact]
    public async Task ShapeFactoryAndHtmlDisplayRenderRealComponentThroughTheResolver()
    {
        var registryServices = new ServiceCollection();
        registryServices.AddLogging();
        registryServices.AddSingleton<IJSRuntime, UnsupportedJSRuntime>();
        var registryServiceProvider = registryServices.BuildServiceProvider();

        var registry = new AssemblyScanningCrestBlazorComponentRegistry([typeof(CrestQuote).Assembly]);
        var resolver = new CrestBlazorComponentShapeBindingResolver(registry, registryServiceProvider, NullLoggerFactory.Instance);

        var shapeTableManager = Substitute.For<IShapeTableManager>();
        shapeTableManager.GetShapeTableAsync(Arg.Any<string>())
            .Returns(new ShapeTable(new Dictionary<string, ShapeDescriptor>(), new Dictionary<string, ShapeBinding>()));

        var themeManager = Substitute.For<IThemeManager>();
        themeManager.GetThemeAsync().Returns(default(OrchardCore.Environment.Extensions.IExtensionInfo));

        var displayServiceProvider = registryServiceProvider;

        // Same IShapeFactory implementation, and the same IHtmlDisplay implementation, a
        // live Orchard request actually uses - only the theme/shape-table dependencies are
        // faked, since a real Blazor-rendered shape never reaches them (resolvers win first).
        var shapeFactory = new DefaultShapeFactory([], shapeTableManager, themeManager, displayServiceProvider);
        var htmlDisplay = new DefaultHtmlDisplay(
            [],
            [resolver],
            shapeTableManager,
            displayServiceProvider,
            new NullLogger<DefaultHtmlDisplay>(),
            Microsoft.Extensions.Options.Options.Create(new OrchardCore.DisplayManagement.ShapeRenderingOptions()),
            themeManager);

        // This is exactly what CrestBlazorComponentPartDisplayDriver.Display's
        // Initialize<CrestBlazorComponentShapeViewModel>(part.ComponentName, model => ...)
        // call resolves to under the hood (DisplayDriverBase.Initialize<TModel> ->
        // ctx.ShapeFactory.CreateAsync<TModel>(shapeType)) - a real dynamic-proxy shape,
        // not a hand-built CrestBlazorComponentShapeViewModel instance. Parameters are
        // copied into Properties, the same way the real driver does it (see its own
        // comment) and the same storage Liquid's shape_new path writes to.
        System.Action<CrestBlazorComponentShapeViewModel> initialize = model =>
            model.Properties["Text"] = "Real content pipeline.";
        var shape = await shapeFactory.CreateAsync("CrestQuote", initialize);

        var html = (await htmlDisplay.ExecuteAsync(new OrchardCore.DisplayManagement.Implementation.DisplayContext
        {
            Value = shape,
            ServiceProvider = displayServiceProvider,
        })).ToString();

        Assert.Contains("rz-quote", html);
        Assert.Contains("Real content pipeline.", html);
    }
}
