using Crest.Iconify;
using Xunit;

namespace Crest.Iconify.Tests;

public sealed class IconifyIconProviderSettingsTests
{
    [Fact]
    public void DefaultIsEnabledAndPointsAtThePublicIconifyApi()
    {
        var settings = IconifyIconProviderSettings.Default;

        Assert.True(settings.Enabled);
        Assert.Equal("https://api.iconify.design", settings.BaseUrl);
        Assert.Null(settings.ApiKey);
        Assert.True(settings.LocalLibraryCacheEnabled);
        Assert.Empty(settings.Prefixes);
    }
}
