using Crest.Admin.Api;
using Crest.Admin.DisplayManagement;
using Xunit;

namespace Crest.Admin.Tests;

// Table-driven coverage of DisplayManager.ResolveCulture's 5-rung priority chain
// (plans/user-localization.md's "Resolution architecture"): session override -> user
// stored default -> admin default (admin-path only) -> browser locale -> tenant default.
public sealed class CultureResolutionTests
{
    private static readonly CultureOption[] Supported =
    [
        new("en", "English", "icon-en"),
        new("es", "Spanish", "icon-es"),
        new("fr", "French", "icon-fr"),
        new("de", "German", "icon-de"),
    ];

    private static CultureSelector Selector(string? userDefault, string tenantDefault, string? adminDefault) =>
        new(userDefault, tenantDefault, adminDefault, Supported, "cookie", "/");

    [Fact]
    public void SessionOverrideWinsOverEverything()
    {
        var selector = Selector(userDefault: "es", tenantDefault: "en", adminDefault: "fr");

        var resolved = DisplayManager.ResolveCulture(selector, sessionOverride: "de", browserLocale: "es", isUnderAdminPath: true);

        Assert.Equal("de", resolved);
    }

    [Fact]
    public void UserDefaultWinsOverAdminDefaultUnderAdminPath()
    {
        var selector = Selector(userDefault: "es", tenantDefault: "en", adminDefault: "fr");

        var resolved = DisplayManager.ResolveCulture(selector, sessionOverride: null, browserLocale: null, isUnderAdminPath: true);

        Assert.Equal("es", resolved);
    }

    [Fact]
    public void AdminDefaultWinsWhenNoOverrideOrUserDefaultAndUnderAdminPath()
    {
        var selector = Selector(userDefault: null, tenantDefault: "en", adminDefault: "fr");

        var resolved = DisplayManager.ResolveCulture(selector, sessionOverride: null, browserLocale: null, isUnderAdminPath: true);

        Assert.Equal("fr", resolved);
    }

    [Fact]
    public void AdminDefaultSkippedWhenNotUnderAdminPath()
    {
        var selector = Selector(userDefault: null, tenantDefault: "en", adminDefault: "fr");

        var resolved = DisplayManager.ResolveCulture(selector, sessionOverride: null, browserLocale: null, isUnderAdminPath: false);

        Assert.Equal("en", resolved);
    }

    [Fact]
    public void BrowserLocaleWinsWhenNothingElseSetAndSupported()
    {
        var selector = Selector(userDefault: null, tenantDefault: "en", adminDefault: null);

        var resolved = DisplayManager.ResolveCulture(selector, sessionOverride: null, browserLocale: "es", isUnderAdminPath: false);

        Assert.Equal("es", resolved);
    }

    [Fact]
    public void BrowserLocaleSkippedWhenNotSupported()
    {
        var selector = Selector(userDefault: null, tenantDefault: "en", adminDefault: null);

        var resolved = DisplayManager.ResolveCulture(selector, sessionOverride: null, browserLocale: "ja-JP", isUnderAdminPath: false);

        Assert.Equal("en", resolved);
    }

    [Fact]
    public void TenantDefaultIsFinalFallback()
    {
        var selector = Selector(userDefault: null, tenantDefault: "en", adminDefault: null);

        var resolved = DisplayManager.ResolveCulture(selector, sessionOverride: null, browserLocale: null, isUnderAdminPath: false);

        Assert.Equal("en", resolved);
    }

    [Fact]
    public void UnsupportedSessionOverrideIsIgnored()
    {
        var selector = Selector(userDefault: "es", tenantDefault: "en", adminDefault: null);

        var resolved = DisplayManager.ResolveCulture(selector, sessionOverride: "ja-JP", browserLocale: null, isUnderAdminPath: false);

        Assert.Equal("es", resolved);
    }

    [Fact]
    public void UnsupportedUserDefaultFallsThroughToAdminDefault()
    {
        var selector = Selector(userDefault: "ja-JP", tenantDefault: "en", adminDefault: "fr");

        var resolved = DisplayManager.ResolveCulture(selector, sessionOverride: null, browserLocale: null, isUnderAdminPath: true);

        Assert.Equal("fr", resolved);
    }

    [Fact]
    public void UnsupportedAdminDefaultFallsThroughToBrowserLocale()
    {
        var selector = Selector(userDefault: null, tenantDefault: "en", adminDefault: "ja-JP");

        var resolved = DisplayManager.ResolveCulture(selector, sessionOverride: null, browserLocale: "fr", isUnderAdminPath: true);

        Assert.Equal("fr", resolved);
    }

    [Theory]
    [InlineData("/admin", "/admin", true)]
    [InlineData("/admin", "/admin/settings/localization", true)]
    [InlineData("/admin", "/administration", false)]
    [InlineData("/admin", "/", false)]
    [InlineData(null, "/admin", false)]
    [InlineData("", "/admin", false)]
    public void IsUnderAdminPathMatchesPrefixNotSubstring(string? basePath, string path, bool expected)
    {
        var resolved = DisplayManager.IsUnderAdminPath(basePath, $"https://example.test{path}");

        Assert.Equal(expected, resolved);
    }
}
