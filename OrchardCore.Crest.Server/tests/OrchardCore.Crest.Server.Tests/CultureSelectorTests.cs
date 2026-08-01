using Crest.Controllers;
using Crest.Services;
using Microsoft.AspNetCore.Http;
using NSubstitute;
using OrchardCore.Environment.Shell;
using OrchardCore.Localization;
using Xunit;

namespace Crest.Server.Tests;

// CultureSelector.FromAsync must return raw inputs only - never a server-resolved
// answer (see plans/user-localization.md's "Resolution architecture": the server cannot
// see the client's sessionStorage session override, so it has no way to resolve culture
// itself; the client is the sole source of truth).
public sealed class CultureSelectorTests
{
    [Fact]
    public async Task ReturnsRawInputsFromLocalizationService()
    {
        var localizationService = Substitute.For<ILocalizationService>();
        localizationService.GetSupportedCulturesAsync().Returns(Task.FromResult(new[] { "en-US", "es-ES", "fr-FR" }));
        localizationService.GetDefaultCultureAsync().Returns(Task.FromResult("en-US"));

        var shellSettings = new ShellSettings { VersionId = "v1" };
        var httpContext = new DefaultHttpContext();

        var selector = await CultureSelector.FromAsync(httpContext, shellSettings, localizationService, userDefaultCulture: "es-ES", adminDefaultCulture: "fr-FR");

        Assert.Equal("es-ES", selector.UserDefaultCulture);
        Assert.Equal("en-US", selector.TenantDefaultCulture);
        Assert.Equal("fr-FR", selector.AdminDefaultCulture);
        Assert.Equal(3, selector.Cultures.Length);
        Assert.Contains(selector.Cultures, c => c.Value == "es-ES");
        Assert.Equal(CrestCultureCookie.MakeCookieName(shellSettings), selector.CookieName);
    }

    [Fact]
    public async Task NullUserAndAdminDefaultsPassThroughAsNull()
    {
        var localizationService = Substitute.For<ILocalizationService>();
        localizationService.GetSupportedCulturesAsync().Returns(Task.FromResult(new[] { "en-US" }));
        localizationService.GetDefaultCultureAsync().Returns(Task.FromResult("en-US"));

        var shellSettings = new ShellSettings { VersionId = "v1" };
        var httpContext = new DefaultHttpContext();

        var selector = await CultureSelector.FromAsync(httpContext, shellSettings, localizationService, userDefaultCulture: null, adminDefaultCulture: null);

        Assert.Null(selector.UserDefaultCulture);
        Assert.Null(selector.AdminDefaultCulture);
    }

    [Fact]
    public void CookieNameIsVersionScopedPerTenant()
    {
        var shellSettings = new ShellSettings { VersionId = "abc123" };

        var name = CrestCultureCookie.MakeCookieName(shellSettings);

        Assert.Equal("crest_culture_abc123", name);
    }

    [Fact]
    public void CookiePathDefaultsToRootWhenNoPathBase()
    {
        var httpContext = new DefaultHttpContext();

        var path = CrestCultureCookie.MakeCookiePath(httpContext);

        Assert.Equal("/", path);
    }

    [Fact]
    public void CookiePathUsesTenantPathBaseWhenPresent()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.PathBase = "/my-tenant";

        var path = CrestCultureCookie.MakeCookiePath(httpContext);

        Assert.Equal("/my-tenant", path);
    }
}
