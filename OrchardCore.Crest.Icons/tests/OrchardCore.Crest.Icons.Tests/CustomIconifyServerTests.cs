using Crest.Iconify;
using NSubstitute;

namespace Crest.Icons.Tests;

/// <summary>
/// plans/icons.md: "custom Iconify server search uses remote HTTP and writes no persistent cache" /
/// "custom Iconify server resolve uses remote HTTP and writes no persistent cache". A custom
/// (non-default) BaseUrl must never route through IIconifyLocalMirrorStore, and must not reuse
/// results across requests via any in-memory cache either - every call is a fresh remote round trip.
/// </summary>
public sealed class CustomIconifyServerTests
{
    private static readonly IconifyIconProviderSettings CustomServerSettings = new(
        Enabled: true,
        BaseUrl: "https://custom-icons.internal.example",
        ApiKey: null,
        ApiKeyHeader: null,
        Prefixes: ["mdi"]);

    [Fact]
    public async Task Resolve_WithCustomServer_NeverConsultsLocalMirrorStore()
    {
        var iconJson = """{"icons":{"home":{"body":"<path d=\"M0 0\"/>"}}}""";
        var (provider, handler, localMirrorStore) = IconifyIconProviderTestFixture.Create(
            CustomServerSettings,
            _ => FakeHttpMessageHandler.JsonResponse(iconJson));

        var result = await provider.ResolveAsync(IconKey.Create("iconify.mdi", "current", "default", "home"), TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.NotEmpty(handler.RequestedUris);
        await localMirrorStore.DidNotReceive().ResolveAsync(Arg.Any<IconifyIconProviderSettings>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Search_WithCustomServer_NeverConsultsLocalMirrorStore()
    {
        var collectionsJson = """{"mdi":{"name":"Material Design Icons","total":1}}""";
        var searchJson = """{"icons":["mdi:home"],"total":1}""";
        var (provider, _, localMirrorStore) = IconifyIconProviderTestFixture.Create(
            CustomServerSettings,
            request => request.RequestUri!.AbsolutePath.Contains("collections")
                ? FakeHttpMessageHandler.JsonResponse(collectionsJson)
                : FakeHttpMessageHandler.JsonResponse(searchJson));

        await provider.SearchAsync(new IconSearchRequest("iconify.mdi", "home", 0, 20), TestContext.Current.CancellationToken);

        await localMirrorStore.DidNotReceive().GetCollectionsAsync(Arg.Any<CancellationToken>());
        await localMirrorStore.DidNotReceive().GetCollectionAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await localMirrorStore.DidNotReceive().ResolveAsync(Arg.Any<IconifyIconProviderSettings>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Resolve_WithCustomServer_DoesNotReuseResultsAcrossCalls_EveryCallHitsRemote()
    {
        var iconJson = """{"icons":{"home":{"body":"<path d=\"M0 0\"/>"}}}""";
        var (provider, handler, _) = IconifyIconProviderTestFixture.Create(
            CustomServerSettings,
            _ => FakeHttpMessageHandler.JsonResponse(iconJson));

        await provider.ResolveAsync(IconKey.Create("iconify.mdi", "current", "default", "home"), TestContext.Current.CancellationToken);
        var firstCallRequestCount = handler.RequestedUris.Count;

        await provider.ResolveAsync(IconKey.Create("iconify.mdi", "current", "default", "home"), TestContext.Current.CancellationToken);

        // Custom servers must be remote-only - a definition cache write would make this a
        // no-op second call. Asserting the request count grew proves no cache short-circuited it.
        Assert.True(handler.RequestedUris.Count > firstCallRequestCount, "Second identical resolve did not hit the remote server - a cache write was made for a custom (non-public) server, which the plan explicitly forbids.");
    }
}
