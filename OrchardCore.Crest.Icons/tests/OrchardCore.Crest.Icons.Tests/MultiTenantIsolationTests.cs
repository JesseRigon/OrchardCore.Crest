using Crest.Iconify;

namespace Crest.Icons.Tests;

/// <summary>
/// plans/icons.md: "two tenants with different public prefix settings share the same local cache
/// but see different visible libraries/results" and "two tenants with custom server settings do
/// not share data or cache entries". Tenant scoping is entirely driven by each tenant's own
/// IIconProviderSettingsStore (Orchard DI-scopes one per tenant) - CrestIconProvidersSettings
/// carries no tenant key itself. So the isolation boundary to prove is: two separately constructed
/// IconifyIconProvider instances, each wired to a different settings/HTTP double, never leak
/// results, requests, or cached state into each other.
/// </summary>
public sealed class MultiTenantIsolationTests
{
    private static readonly IconifyIconProviderSettings TenantAPublicSettings = new(
        Enabled: true,
        BaseUrl: IconifyIconProviderSettings.Default.BaseUrl,
        ApiKey: null,
        ApiKeyHeader: null,
        Prefixes: ["mdi"]);

    private static readonly IconifyIconProviderSettings TenantBPublicSettings = new(
        Enabled: true,
        BaseUrl: IconifyIconProviderSettings.Default.BaseUrl,
        ApiKey: null,
        ApiKeyHeader: null,
        Prefixes: ["lucide"]);

    private static readonly IconifyIconProviderSettings TenantACustomSettings = new(
        Enabled: true,
        BaseUrl: "https://tenant-a-icons.internal.example",
        ApiKey: "tenant-a-secret",
        ApiKeyHeader: null,
        Prefixes: ["mdi"]);

    private static readonly IconifyIconProviderSettings TenantBCustomSettings = new(
        Enabled: true,
        BaseUrl: "https://tenant-b-icons.internal.example",
        ApiKey: "tenant-b-secret",
        ApiKeyHeader: null,
        Prefixes: ["lucide"]);

    [Fact]
    public async Task TwoTenants_WithDifferentPublicPrefixes_SeeOnlyTheirOwnAllowedLibraries()
    {
        var (providerA, _, _) = IconifyIconProviderTestFixture.Create(TenantAPublicSettings);
        var (providerB, _, _) = IconifyIconProviderTestFixture.Create(TenantBPublicSettings);

        var librariesA = await providerA.GetLibrariesAsync(TestContext.Current.CancellationToken);
        var librariesB = await providerB.GetLibrariesAsync(TestContext.Current.CancellationToken);

        Assert.Contains(librariesA, library => library.Id == "iconify.mdi");
        Assert.DoesNotContain(librariesA, library => library.Id == "iconify.lucide");

        Assert.Contains(librariesB, library => library.Id == "iconify.lucide");
        Assert.DoesNotContain(librariesB, library => library.Id == "iconify.mdi");
    }

    [Fact]
    public async Task TwoTenants_WithCustomServers_NeverSendRequestsToEachOthersServer()
    {
        var (providerA, handlerA, _) = IconifyIconProviderTestFixture.Create(
            TenantACustomSettings,
            _ => FakeHttpMessageHandler.JsonResponse("""{"icons":{"home":{"body":"<path/>"}}}"""));
        var (providerB, handlerB, _) = IconifyIconProviderTestFixture.Create(
            TenantBCustomSettings,
            _ => FakeHttpMessageHandler.JsonResponse("""{"icons":{"home":{"body":"<path/>"}}}"""));

        await providerA.ResolveAsync(IconKey.Create("iconify.mdi", "current", "default", "home"), TestContext.Current.CancellationToken);
        await providerB.ResolveAsync(IconKey.Create("iconify.lucide", "current", "default", "home"), TestContext.Current.CancellationToken);

        Assert.All(handlerA.RequestedUris, uri => Assert.Equal("tenant-a-icons.internal.example", uri.Host));
        Assert.All(handlerB.RequestedUris, uri => Assert.Equal("tenant-b-icons.internal.example", uri.Host));
    }

    [Fact]
    public async Task TwoTenants_WithCustomServers_DoNotShareApiKeys()
    {
        HttpRequestMessage? capturedRequestA = null;
        HttpRequestMessage? capturedRequestB = null;

        var (providerA, _, _) = IconifyIconProviderTestFixture.Create(
            TenantACustomSettings with { ApiKeyHeader = "Authorization" },
            request =>
            {
                capturedRequestA = request;
                return FakeHttpMessageHandler.JsonResponse("""{"icons":{"home":{"body":"<path/>"}}}""");
            });
        var (providerB, _, _) = IconifyIconProviderTestFixture.Create(
            TenantBCustomSettings with { ApiKeyHeader = "Authorization" },
            request =>
            {
                capturedRequestB = request;
                return FakeHttpMessageHandler.JsonResponse("""{"icons":{"home":{"body":"<path/>"}}}""");
            });

        await providerA.ResolveAsync(IconKey.Create("iconify.mdi", "current", "default", "home"), TestContext.Current.CancellationToken);
        await providerB.ResolveAsync(IconKey.Create("iconify.lucide", "current", "default", "home"), TestContext.Current.CancellationToken);

        var authHeaderA = capturedRequestA?.Headers.GetValues("Authorization").Single();
        var authHeaderB = capturedRequestB?.Headers.GetValues("Authorization").Single();

        Assert.Equal("Bearer tenant-a-secret", authHeaderA);
        Assert.Equal("Bearer tenant-b-secret", authHeaderB);
        Assert.NotEqual(authHeaderA, authHeaderB);
    }

    [Fact]
    public async Task TwoTenants_WithSamePublicSettings_DoNotShareInMemoryCacheInstances()
    {
        var requestCountA = 0;
        var requestCountB = 0;
        var (providerA, _, _) = IconifyIconProviderTestFixture.Create(
            TenantAPublicSettings,
            _ => { requestCountA++; return FakeHttpMessageHandler.JsonResponse("""{"icons":{"home":{"body":"<path/>"}}}"""); });
        var (providerB, _, _) = IconifyIconProviderTestFixture.Create(
            TenantAPublicSettings,
            _ => { requestCountB++; return FakeHttpMessageHandler.JsonResponse("""{"icons":{"home":{"body":"<path/>"}}}"""); });

        await providerA.ResolveAsync(IconKey.Create("iconify.mdi", "current", "default", "home"), TestContext.Current.CancellationToken);
        await providerA.ResolveAsync(IconKey.Create("iconify.mdi", "current", "default", "home"), TestContext.Current.CancellationToken);

        // Provider A's second call should be served from its own definition cache (no new
        // request); Provider B, a distinct instance, must still see a cache miss on its first call.
        Assert.Equal(1, requestCountA);
        await providerB.ResolveAsync(IconKey.Create("iconify.mdi", "current", "default", "home"), TestContext.Current.CancellationToken);
        Assert.Equal(1, requestCountB);
    }
}
