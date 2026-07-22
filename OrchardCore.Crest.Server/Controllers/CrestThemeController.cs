using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrchardCore.Settings;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Crest.Controllers;

[ApiController]
[AutoValidateAntiforgeryToken]
[Route("api/crest/theme")]
public sealed class CrestThemeController(ISiteService siteService, IAuthorizationService authorizationService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var site = await siteService.GetSiteSettingsAsync();
        var settings = site.Properties["CrestTheme"]?.Deserialize<CrestThemeSettings>(JsonSerializerOptions.Web)
            ?? CrestThemeSettings.Default;

        return Ok(settings);
    }

    [HttpPut]
    public async Task<ActionResult<CrestThemeSettings>> Put(CrestThemeSettings settings)
    {
        if (!await authorizationService.AuthorizeAsync(User, SettingsPermissions.ManageSettings))
        {
            return Forbid();
        }

        var normalized = new CrestThemeSettings(
            string.IsNullOrWhiteSpace(settings.RadzenTheme) ? CrestThemeSettings.Default.RadzenTheme : settings.RadzenTheme.Trim(),
            settings.Tokens
                .Where(token => !string.IsNullOrWhiteSpace(token.Key) && !string.IsNullOrWhiteSpace(token.Value))
                .ToDictionary(token => token.Key.Trim(), token => token.Value.Trim(), StringComparer.OrdinalIgnoreCase));

        var site = await siteService.LoadSiteSettingsAsync();
        site.Properties["CrestTheme"] = JsonSerializer.SerializeToNode(normalized, JsonSerializerOptions.Web) ?? new JsonObject();
        await siteService.UpdateSiteSettingsAsync(site);

        return Ok(normalized);
    }
}

public sealed record CrestThemeSettings(string RadzenTheme, Dictionary<string, string> Tokens)
{
    public static CrestThemeSettings Default { get; } = new(
        "material-base",
        new Dictionary<string, string>
        {
            ["primary"] = "#2f6f4e",
            ["secondary"] = "#6d5d3f",
            ["surface"] = "#ffffff",
            ["background"] = "#f7f8f6",
            ["radius"] = "6px",
        });
}
