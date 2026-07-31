using Crest.Iconify;
using NSubstitute;

namespace Crest.Icons.Tests;

internal static class IconifyIconProviderTestFixture
{
    public static (IconifyIconProvider Provider, FakeHttpMessageHandler Handler, IIconifyLocalMirrorStore LocalMirrorStore) Create(
        IconifyIconProviderSettings settings,
        Func<HttpRequestMessage, HttpResponseMessage>? respond = null)
    {
        var handler = new FakeHttpMessageHandler(respond ?? (_ => FakeHttpMessageHandler.JsonResponse("""{"icons":{}}""")));
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri(settings.BaseUrl) };

        var httpClientFactory = Substitute.For<IHttpClientFactory>();
        httpClientFactory.CreateClient(Arg.Any<string>()).Returns(httpClient);

        var settingsStore = Substitute.For<IIconProviderSettingsStore>();
        settingsStore.GetAsync(Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(new CrestIconProvidersSettings(settings)));

        var localMirrorStore = Substitute.For<IIconifyLocalMirrorStore>();
        localMirrorStore.IsPublicIconify(Arg.Any<IconifyIconProviderSettings>())
            .Returns(callInfo => string.Equals(
                NormalizeBaseUrl(callInfo.Arg<IconifyIconProviderSettings>()?.BaseUrl),
                NormalizeBaseUrl(IconifyIconProviderSettings.Default.BaseUrl),
                StringComparison.OrdinalIgnoreCase));
        localMirrorStore.GetCollectionsAsync(Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<IReadOnlyDictionary<string, IconifyLocalCollectionInfo>>(
                new Dictionary<string, IconifyLocalCollectionInfo>(StringComparer.OrdinalIgnoreCase)));
        localMirrorStore.ResolveAsync(Arg.Any<IconifyIconProviderSettings>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<IconifyLocalIcon?>(null));

        var provider = new IconifyIconProvider(httpClientFactory, settingsStore, localMirrorStore, new SvgIconSanitizer());
        return (provider, handler, localMirrorStore);
    }

    private static string NormalizeBaseUrl(string? baseUrl)
    {
        var value = string.IsNullOrWhiteSpace(baseUrl) ? IconifyIconProviderSettings.Default.BaseUrl : baseUrl.Trim();
        return value.TrimEnd('/');
    }
}
