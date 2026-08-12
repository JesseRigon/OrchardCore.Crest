using Crest.Blazor;
using Crest.Blazor.Models;
using Crest.Components.Primitives;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using OrchardCore.DisplayManagement.Implementation;
using Xunit;

namespace Crest.Server.Tests;

// Exercises the actual end-to-end path: registry resolves a real component by its
// [CrestBlazorComponent] name, and the resolver renders it via HtmlRenderer to real HTML -
// not just "no exception thrown". Asserting on rendered content (not just a clean run) is
// what caught the real ParametersFromShape bug this suite found during development (see
// the integration test in this same project for that history).
public sealed class CrestBlazorComponentShapeBindingResolverTests
{
    [Fact]
    public void RegistryResolvesRealMarkedComponent()
    {
        var registry = new AssemblyScanningCrestBlazorComponentRegistry([typeof(CrestHeading).Assembly]);

        Assert.True(registry.TryResolve("CrestHeading", out var type));
        Assert.Equal(typeof(CrestHeading), type);
    }

    [Fact]
    public void RegistryFallsThroughForUnknownName()
    {
        var registry = new AssemblyScanningCrestBlazorComponentRegistry([typeof(CrestHeading).Assembly]);

        Assert.False(registry.TryResolve("NotARegisteredComponent", out _));
    }

    [Fact]
    public async Task ResolverRendersRealHtmlForRegisteredShapeType()
    {
        var registry = new AssemblyScanningCrestBlazorComponentRegistry([typeof(CrestHeading).Assembly]);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IJSRuntime, UnsupportedJSRuntime>();
        var serviceProvider = services.BuildServiceProvider();

        var resolver = new CrestBlazorComponentShapeBindingResolver(registry, serviceProvider, NullLoggerFactory.Instance);

        var binding = await resolver.GetShapeBindingAsync("CrestHeading");

        Assert.NotNull(binding);

        // The resolver reads component parameters from IShape.Properties, not from this
        // model's own Parameters CLR property (see the resolver's own comment on why -
        // Composite's dynamic-property machinery never sees a plain auto-property). A
        // CrestBlazorComponentPartDisplayDriver-built shape copies Parameters into
        // Properties explicitly for this reason; do the same here.
        var shape = new CrestBlazorComponentShapeViewModel();
        shape.Properties["Text"] = "Hello Crest";
        var displayContext = new DisplayContext { Value = shape };

        var html = (await binding.BindingAsync(displayContext)).ToString();

        Assert.Contains("rz-heading", html);
        Assert.Contains("Hello Crest", html);
    }

    [Fact]
    public async Task ResolverFallsThroughForUnregisteredShapeType()
    {
        var registry = new AssemblyScanningCrestBlazorComponentRegistry([typeof(CrestHeading).Assembly]);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IJSRuntime, UnsupportedJSRuntime>();
        var serviceProvider = services.BuildServiceProvider();

        var resolver = new CrestBlazorComponentShapeBindingResolver(registry, serviceProvider, NullLoggerFactory.Instance);

        var binding = await resolver.GetShapeBindingAsync("Content-Page");

        Assert.Null(binding);
    }
}
