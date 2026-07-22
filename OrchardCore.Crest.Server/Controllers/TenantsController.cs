using Crest.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using OrchardCore.Environment.Shell;
using OrchardCore.Environment.Shell.Removing;
using OrchardCore.Tenants;

namespace Crest.Controllers;

[ApiController]
[AutoValidateAntiforgeryToken]
[Route("api/crest/tenants")]
public sealed class CrestTenantsController(ICrestRequestAccess requestAccess) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<CrestTenantCatalog>> ListAsync(
        [FromQuery] string? search = null,
        [FromQuery] string? category = null,
        [FromQuery] string? state = null,
        [FromQuery] string? orderBy = null)
    {
        var access = await GetDefaultTenantAccessAsync();
        if (access is null) return Forbid();

        var settings = access.GetRequiredService<IShellHost>().GetAllSettings();
        var entries = settings.Select(CrestTenant.From);
        if (!string.IsNullOrWhiteSpace(search))
        {
            entries = entries.Where(tenant => tenant.Name.Contains(search, StringComparison.OrdinalIgnoreCase)
                || tenant.RequestUrlHost?.Contains(search, StringComparison.OrdinalIgnoreCase) == true
                || tenant.RequestUrlPrefix?.Contains(search, StringComparison.OrdinalIgnoreCase) == true);
        }
        if (!string.IsNullOrWhiteSpace(category)) entries = entries.Where(tenant => string.Equals(tenant.Category, category, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(state) && !string.Equals(state, "All", StringComparison.OrdinalIgnoreCase)) entries = entries.Where(tenant => string.Equals(tenant.State, state, StringComparison.OrdinalIgnoreCase));

        entries = string.Equals(orderBy, "State", StringComparison.OrdinalIgnoreCase)
            ? entries.OrderBy(tenant => tenant.State, StringComparer.OrdinalIgnoreCase).ThenBy(tenant => tenant.Name, StringComparer.OrdinalIgnoreCase)
            : entries.OrderBy(tenant => tenant.Name, StringComparer.OrdinalIgnoreCase);

        var categories = settings.Select(setting => setting["Category"])
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var removalAllowed = access.GetRequiredService<IOptions<TenantsOptions>>().Value.TenantRemovalAllowed;
        return Ok(new CrestTenantCatalog(entries.ToArray(), categories, removalAllowed));
    }

    [HttpPost("{name}/enable")]
    public Task<ActionResult<CrestTenant>> EnableAsync(string name) => ChangeStateAsync(name, "Enable");

    [HttpPost("{name}/disable")]
    public Task<ActionResult<CrestTenant>> DisableAsync(string name) => ChangeStateAsync(name, "Disable");

    [HttpPost("bulk")]
    public async Task<ActionResult<CrestTenant[]>> BulkAsync([FromBody] CrestTenantBulkAction request)
    {
        if (!string.Equals(request.Action, "Enable", StringComparison.OrdinalIgnoreCase) && !string.Equals(request.Action, "Disable", StringComparison.OrdinalIgnoreCase)) return BadRequest("Only Enable and Disable are valid tenant bulk actions.");
        var access = await GetDefaultTenantAccessAsync();
        if (access is null) return Forbid();

        var host = access.GetRequiredService<IShellHost>();
        var updated = new List<CrestTenant>();
        foreach (var name in request.Names?.Distinct(StringComparer.OrdinalIgnoreCase) ?? [])
        {
            if (!host.TryGetSettings(name, out var settings)) continue;
            if (string.Equals(request.Action, "Enable", StringComparison.OrdinalIgnoreCase) && settings.IsDisabled())
            {
                await host.UpdateShellSettingsAsync(settings.AsRunning());
                updated.Add(CrestTenant.From(settings.AsRunning()));
            }
            else if (string.Equals(request.Action, "Disable", StringComparison.OrdinalIgnoreCase) && !settings.IsDefaultShell() && settings.IsRunning())
            {
                await host.UpdateShellSettingsAsync(settings.AsDisabled());
                updated.Add(CrestTenant.From(settings.AsDisabled()));
            }
        }
        return Ok(updated.ToArray());
    }

    [HttpPost("{name}/reload")]
    public async Task<IActionResult> ReloadAsync(string name)
    {
        var access = await GetDefaultTenantAccessAsync();
        if (access is null) return Forbid();
        var host = access.GetRequiredService<IShellHost>();
        if (!host.TryGetSettings(name, out var settings)) return NotFound();
        await host.ReloadShellContextAsync(settings);
        return NoContent();
    }

    [HttpDelete("{name}")]
    public async Task<IActionResult> RemoveAsync(string name)
    {
        var access = await GetDefaultTenantAccessAsync();
        if (access is null) return Forbid();
        if (!access.GetRequiredService<IOptions<TenantsOptions>>().Value.TenantRemovalAllowed) return Forbid();

        var host = access.GetRequiredService<IShellHost>();
        if (!host.TryGetSettings(name, out var settings)) return NotFound();
        if (!settings.IsRemovable()) return BadRequest("Only Disabled or Uninitialized tenants can be removed.");

        var result = await access.GetRequiredService<IShellRemovalManager>().RemoveAsync(settings);
        return result.Success ? NoContent() : BadRequest(result.ErrorMessage);
    }

    private async Task<ActionResult<CrestTenant>> ChangeStateAsync(string name, string action)
    {
        var access = await GetDefaultTenantAccessAsync();
        if (access is null) return Forbid();
        var host = access.GetRequiredService<IShellHost>();
        if (!host.TryGetSettings(name, out var settings)) return NotFound();

        if (string.Equals(action, "Enable", StringComparison.OrdinalIgnoreCase))
        {
            if (!settings.IsDisabled()) return BadRequest("Only Disabled tenants can be enabled.");
            var enabled = settings.AsRunning();
            await host.UpdateShellSettingsAsync(enabled);
            return Ok(CrestTenant.From(enabled));
        }

        if (settings.IsDefaultShell()) return BadRequest("The default tenant cannot be disabled.");
        if (!settings.IsRunning()) return BadRequest("Only Running tenants can be disabled.");
        var disabled = settings.AsDisabled();
        await host.UpdateShellSettingsAsync(disabled);
        return Ok(CrestTenant.From(disabled));
    }

    private async Task<CrestAuthorizedRequest?> GetDefaultTenantAccessAsync()
    {
        var access = await requestAccess.AuthorizeAsync(User, Permissions.ManageTenants);
        if (access is null || !access.GetRequiredService<ShellSettings>().IsDefaultShell()) return null;
        return access;
    }
}

public sealed record CrestTenantCatalog(CrestTenant[] Tenants, string[] Categories, bool TenantRemovalAllowed);
public sealed record CrestTenantBulkAction(string Action, string[]? Names);
public sealed record CrestTenant(string Name, string State, string? Category, string? Description, string? RequestUrlHost, string? RequestUrlPrefix, bool IsDefault, bool IsRemovable)
{
    public static CrestTenant From(ShellSettings settings) => new(settings.Name, settings.State.ToString(), settings["Category"], settings["Description"], settings.RequestUrlHost, settings.RequestUrlPrefix, settings.IsDefaultShell(), settings.IsRemovable());
}
