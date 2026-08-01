using System.Globalization;
using System.Security.Claims;
using System.Text.Json.Nodes;
using Crest.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using NSubstitute;
using OrchardCore.Entities;
using OrchardCore.Environment.Shell;
using OrchardCore.Localization;
using OrchardCore.Settings;
using OrchardCore.Users;
using Xunit;

namespace Crest.Server.Tests;

// ISite's concrete OrchardCore.Settings.SiteSettings implementation lives in the
// OrchardCore.Settings module, which the Crest server project doesn't reference (only
// the ISite/ISiteService abstractions). A minimal fake is enough here: the controller
// only ever calls TryGet<T>/Alter<T> (Entity extension methods driven by ISite.Properties)
// against it, never any of the other ISite members.
internal sealed class FakeSite : ISite
{
    public JsonObject Properties { get; set; } = [];
    public string SiteName { get; set; } = "Test";
    public string PageTitleFormat { get; set; } = "";
    public string SiteSalt { get; set; } = "";
    public string SuperUser { get; set; } = "";
    public string Calendar { get; set; } = "";
    public string TimeZoneId { get; set; } = "";
    public ResourceDebugMode ResourceDebugMode { get; set; }
    public bool UseCdn { get; set; }
    public string CdnBaseUrl { get; set; } = "";
    public int PageSize { get; set; }
    public int MaxPageSize { get; set; }
    public int MaxPagedCount { get; set; }
    public string BaseUrl { get; set; } = "";
    public RouteValueDictionary HomeRoute { get; set; } = [];
    public bool AppendVersion { get; set; }
    public CacheMode CacheMode { get; set; }

    public T As<T>() where T : new() => GetOrCreate<T>();

    public T GetOrCreate<T>() where T : new() => TryGet<T>(out var settings) ? settings : new T();

    public bool TryGet<T>(out T settings) => this.TryGet(typeof(T).Name, out settings);
}

// LocalizationController.SaveAsync validates AdminDefaultCulture (rung 3 of the client
// resolution chain - plans/user-localization.md) must be one of the tenant's supported
// cultures, or null to clear the override. It must never let an admin default force an
// unsupported culture onto the admin area.
public sealed class LocalizationControllerSaveTests
{
    private static CrestLocalizationController BuildController(FakeSite site, out ISiteService sites)
    {
        sites = Substitute.For<ISiteService>();
        sites.LoadSiteSettingsAsync().Returns(Task.FromResult<ISite>(site));
        sites.GetSiteSettingsAsync().Returns(Task.FromResult<ISite>(site));

        var releases = Substitute.For<IShellReleaseManager>();
        var authorization = Substitute.For<IAuthorizationService>();
        authorization
            .AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<object>(), Arg.Any<IEnumerable<IAuthorizationRequirement>>())
            .Returns(Task.FromResult(AuthorizationResult.Success()));

        var localizationService = Substitute.For<ILocalizationService>();
        localizationService.GetAllCulturesAndAliases().Returns(CultureInfo.GetCultures(CultureTypes.AllCultures));

        var userManager = Substitute.For<UserManager<IUser>>(
            Substitute.For<IUserStore<IUser>>(), null!, null!, null!, null!, null!, null!, null!, null!);
        var localizationManager = Substitute.For<ILocalizationManager>();

        var controller = new CrestLocalizationController(sites, releases, authorization, localizationService, userManager, localizationManager)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) },
            },
        };

        return controller;
    }

    [Fact]
    public async Task RejectsAdminDefaultCultureNotInSupportedCultures()
    {
        var site = new FakeSite();
        var controller = BuildController(site, out _);

        var result = await controller.SaveAsync(new CrestLocalization(
            "en-US",
            ["en-US", "es-ES"],
            false,
            "fr-FR",
            []));

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task AcceptsNullAdminDefaultCultureAsClearingTheOverride()
    {
        var site = new FakeSite();
        var controller = BuildController(site, out var sites);

        var result = await controller.SaveAsync(new CrestLocalization(
            "en-US",
            ["en-US", "es-ES"],
            false,
            null,
            []));

        Assert.IsType<OkObjectResult>(result.Result);
        await sites.Received(1).UpdateSiteSettingsAsync(Arg.Any<ISite>());
        Assert.True(site.TryGet<CrestLocalizationSettings>(out var settings));
        Assert.Null(settings.AdminDefaultCulture);
    }

    [Fact]
    public async Task AcceptsAdminDefaultCultureThatIsSupported()
    {
        var site = new FakeSite();
        var controller = BuildController(site, out var sites);

        var result = await controller.SaveAsync(new CrestLocalization(
            "en-US",
            ["en-US", "es-ES", "fr-FR"],
            false,
            "fr-FR",
            []));

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<CrestLocalization>(ok.Value);
        Assert.Equal("fr-FR", dto.AdminDefaultCulture);
        await sites.Received(1).UpdateSiteSettingsAsync(Arg.Any<ISite>());
        Assert.True(site.TryGet<CrestLocalizationSettings>(out var settings));
        Assert.Equal("fr-FR", settings.AdminDefaultCulture);
    }

    [Fact]
    public async Task RejectsEmptySupportedCultures()
    {
        var site = new FakeSite();
        var controller = BuildController(site, out _);

        var result = await controller.SaveAsync(new CrestLocalization(
            "en-US",
            [],
            false,
            null,
            []));

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task FallsBackDefaultCultureToFirstSupportedWhenNotInList()
    {
        var site = new FakeSite();
        var controller = BuildController(site, out _);

        var result = await controller.SaveAsync(new CrestLocalization(
            "ja-JP",
            ["en-US", "es-ES"],
            false,
            null,
            []));

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<CrestLocalization>(ok.Value);
        Assert.Equal("en-US", dto.DefaultCulture);
    }
}
