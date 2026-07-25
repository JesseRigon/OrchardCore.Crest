using System.Text.Json;
using Crest.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Crest.Controllers;

[ApiController]
[AutoValidateAntiforgeryToken]
[Route("api/crest/admin-menu-layout")]
public sealed class CrestAdminMenuLayoutExportController(
    IAuthorizationService authorizationService,
    IConfiguration configuration,
    IWebHostEnvironment environment,
    CrestAdminMenuLayoutService layoutService) : ControllerBase
{
    public const string DefaultFileName = "crest-admin-menu-layout.json";
    private const string ExportEnabledKey = "Crest:AdminMenuLayoutExport:Enabled";
    private const string RecipesDirectoryName = "recipes";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    [HttpPost("export")]
    public async Task<IActionResult> ExportAsync([FromQuery] string? file = null)
    {
        if (!environment.IsDevelopment() && !configuration.GetValue<bool>(ExportEnabledKey))
        {
            return NotFound();
        }

        if (!await authorizationService.AuthorizeAsync(User, OrchardCore.AdminMenu.AdminMenuPermissions.ManageAdminMenu))
        {
            return Forbid();
        }

        var fileName = string.IsNullOrWhiteSpace(file) ? DefaultFileName : Path.GetFileName(file);
        if (!string.Equals(fileName, file ?? fileName, StringComparison.Ordinal))
        {
            return BadRequest("Only a file name is allowed.");
        }

        var recipesPath = Path.Combine(environment.ContentRootPath, RecipesDirectoryName);
        Directory.CreateDirectory(recipesPath);

        var outputPath = Path.Combine(recipesPath, fileName);
        var layout = await layoutService.ExportAsync();
        await System.IO.File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(layout, JsonOptions) + Environment.NewLine, HttpContext.RequestAborted);

        return Ok(new
        {
            file = fileName,
            path = outputPath,
            relativePath = Path.Combine(RecipesDirectoryName, fileName).Replace('\\', '/'),
            itemCount = layout.Items.Count,
            customItemCount = layout.CustomItems.Count,
            separatorCount = layout.Separators.Count,
        });
    }
}
