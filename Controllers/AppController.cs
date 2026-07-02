using BlazingOrchard.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.Environment.Extensions;
using OrchardCore.Environment.Shell;
using OrchardCore.Environment.Shell.Descriptor;
using OrchardCore.Navigation;
using OrchardCore.Settings;
using System.Security.Cryptography;
using System.Text;

namespace BlazingOrchard.Controllers;

[ApiController]
[IgnoreAntiforgeryToken]
[Route("api/blazing/app")]
public sealed class AppController(
    ShellSettings shellSettings,
    IShellDescriptorManager shellDescriptorManager,
    IExtensionManager extensionManager,
    ISiteService siteService,
    INavigationManager navigationManager,
    IOptions<AdminOptions> adminOptions,
    BlazingAdminMenuLayoutService layoutService) : ControllerBase
{
    [HttpGet("manifest")]
    public async Task<ActionResult<AppManifest>> GetManifest()
    {
        var descriptor = await shellDescriptorManager.GetShellDescriptorAsync();
        var site = await siteService.GetSiteSettingsAsync();
        var featureIds = descriptor.Features.Select(feature => feature.Id).Order(StringComparer.Ordinal).ToArray();
        var featureInfos = extensionManager.GetFeatures(featureIds.AsEnumerable()).ToDictionary(feature => feature.Id);
        var adminItems = await navigationManager.BuildMenuAsync("admin", ControllerContext);
        var adminMenu = await layoutService.ApplyAsync(new NavigationMenu("admin", adminItems.OrderBy(item => item.Position, NavigationPositionComparer.Instance)
            .Select(NavigationItem.From)
            .ToArray()));

        return Ok(new AppManifest(
            Tenant.From(shellSettings),
            SiteSettings.From(site),
            new AdminDescriptor(NormalizeAdminPath(adminOptions.Value.AdminUrlPrefix)),
            descriptor.SerialNumber,
            ComputeFeatureHash(descriptor.SerialNumber, featureIds),
            featureIds.Select(id => Feature.From(id, featureInfos.GetValueOrDefault(id))).ToArray(),
            adminMenu));
    }

    private static string NormalizeAdminPath(string? adminUrlPrefix)
    {
        var prefix = string.IsNullOrWhiteSpace(adminUrlPrefix) ? "admin" : adminUrlPrefix.Trim('/');
        return '/' + prefix;
    }

    private static string ComputeFeatureHash(int serialNumber, IEnumerable<string> featureIds)
    {
        var input = $"{serialNumber}:{string.Join('|', featureIds)}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input))).ToLowerInvariant();
    }
}

public sealed record AppManifest(
    Tenant Tenant,
    SiteSettings Site,
    AdminDescriptor Admin,
    int FeatureSerialNumber,
    string FeatureHash,
    Feature[] Features,
    NavigationMenu AdminMenu);

public sealed record Tenant(
    string Name,
    string TenantId,
    string State,
    string? RequestUrlHost,
    string[] RequestUrlHosts,
    string? RequestUrlPrefix)
{
    public static Tenant From(ShellSettings settings) => new(
        settings.Name,
        settings.TenantId,
        settings.State.ToString(),
        settings.RequestUrlHost,
        settings.RequestUrlHosts ?? [],
        settings.RequestUrlPrefix);
}

public sealed record AdminDescriptor(string BasePath);
