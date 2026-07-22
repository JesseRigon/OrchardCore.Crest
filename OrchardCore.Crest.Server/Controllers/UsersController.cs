using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Crest.Services;
using OrchardCore.Users;
using OrchardCore.Users.Models;
using OrchardCore.Users.Services;

namespace Crest.Controllers;

[ApiController]
[AutoValidateAntiforgeryToken]
[Route("api/crest/users")]
public sealed class CrestUsersController(UserManager<IUser> userManager, IUserService users, IAuthorizationService authorization, ICrestPermissionInvalidator permissions) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<CrestUserList>> ListAsync([FromQuery] string? search, [FromQuery] string? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        if (!await authorization.AuthorizeAsync(User, UsersPermissions.ListUsers, new User())) return Forbid();
        var all = userManager.Users.Cast<User>();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            all = all.Where(user => (user.UserName ?? string.Empty).Contains(term, StringComparison.OrdinalIgnoreCase) || (user.Email ?? string.Empty).Contains(term, StringComparison.OrdinalIgnoreCase));
        }
        if (string.Equals(status, "enabled", StringComparison.OrdinalIgnoreCase)) all = all.Where(user => user.IsEnabled);
        if (string.Equals(status, "disabled", StringComparison.OrdinalIgnoreCase)) all = all.Where(user => !user.IsEnabled);
        var matching = all.OrderBy(user => user.UserName).ToArray();
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 100);
        return Ok(new CrestUserList(matching.Length, matching.Skip((page - 1) * pageSize).Take(pageSize).Select(CrestUser.From).ToArray()));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<CrestUser>> GetAsync(string id)
    {
        var user = await FindAsync(id); if (user is null) return NotFound();
        if (!await authorization.AuthorizeAsync(User, UsersPermissions.ViewUsers, user)) return Forbid();
        return Ok(CrestUser.From(user));
    }

    [HttpPost]
    public async Task<ActionResult<CrestUser>> CreateAsync([FromBody] CrestUserWrite request)
    {
        var user = new User();
        if (!await authorization.AuthorizeAsync(User, UsersPermissions.EditUsers, user)) return Forbid();
        if (string.IsNullOrWhiteSpace(request.Password)) return BadRequest("A password is required.");
        Apply(request, user);
        var result = await userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded) return BadRequest(result.Errors.Select(error => error.Description));
        await SetRolesAsync(user, request.Roles);
        await permissions.InvalidateTenantAsync(HttpContext.RequestAborted);
        return Created($"api/crest/users/{user.UserId}", CrestUser.From(user));
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<CrestUser>> SaveAsync(string id, [FromBody] CrestUserWrite request)
    {
        var user = await FindAsync(id); if (user is null) return NotFound();
        if (!await authorization.AuthorizeAsync(User, UsersPermissions.EditUsers, user)) return Forbid();
        Apply(request, user);
        var result = await userManager.UpdateAsync(user);
        if (!result.Succeeded) return BadRequest(result.Errors.Select(error => error.Description));
        if (!string.IsNullOrWhiteSpace(request.Password))
        {
            var token = await userManager.GeneratePasswordResetTokenAsync(user);
            result = await userManager.ResetPasswordAsync(user, token, request.Password);
            if (!result.Succeeded) return BadRequest(result.Errors.Select(error => error.Description));
        }
        await SetRolesAsync(user, request.Roles);
        await permissions.InvalidateTenantAsync(HttpContext.RequestAborted);
        return Ok(CrestUser.From(user));
    }

    [HttpPost("{id}/enabled")]
    public async Task<ActionResult<CrestUser>> SetEnabledAsync(string id, [FromBody] CrestUserEnabled request)
    {
        var user = await FindAsync(id); if (user is null) return NotFound();
        if (!await authorization.AuthorizeAsync(User, UsersPermissions.EditUsers, user)) return Forbid();
        if (User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value == user.UserId && !request.Enabled) return BadRequest("You cannot disable your own account.");
        var changed = request.Enabled ? await users.EnableAsync(user) : await users.DisableAsync(user);
        if (changed) await permissions.InvalidateTenantAsync(HttpContext.RequestAborted);
        return changed ? Ok(CrestUser.From(user)) : BadRequest("The user status could not be changed.");
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteAsync(string id)
    {
        var user = await FindAsync(id); if (user is null) return NotFound();
        if (!await authorization.AuthorizeAsync(User, UsersPermissions.DeleteUsers, user)) return Forbid();
        if (User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value == user.UserId) return BadRequest("You cannot delete your own account.");
        var result = await userManager.DeleteAsync(user);
        if (result.Succeeded) await permissions.InvalidateTenantAsync(HttpContext.RequestAborted);
        return result.Succeeded ? NoContent() : BadRequest(result.Errors.Select(error => error.Description));
    }

    private async Task<User?> FindAsync(string id) => await userManager.FindByIdAsync(id) as User;
    private static void Apply(CrestUserWrite request, User user) { user.UserName = request.UserName?.Trim(); user.Email = request.Email?.Trim(); user.PhoneNumber = request.PhoneNumber?.Trim(); user.EmailConfirmed = request.EmailConfirmed; user.IsEnabled = request.IsEnabled; }
    private async Task SetRolesAsync(User user, string[]? roles)
    {
        var target = (roles ?? []).Where(role => !string.IsNullOrWhiteSpace(role)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var current = await userManager.GetRolesAsync(user);
        var remove = current.Except(target, StringComparer.OrdinalIgnoreCase).ToArray();
        var add = target.Except(current, StringComparer.OrdinalIgnoreCase).ToArray();
        if (remove.Length > 0) await userManager.RemoveFromRolesAsync(user, remove);
        if (add.Length > 0) await userManager.AddToRolesAsync(user, add);
    }
}

public sealed record CrestUserList(int Total, CrestUser[] Items);
public sealed record CrestUser(string Id, string? UserName, string? Email, string? PhoneNumber, bool EmailConfirmed, bool IsEnabled, bool TwoFactorEnabled, string[] Roles)
{
    public static CrestUser From(User user) => new(user.UserId, user.UserName, user.Email, user.PhoneNumber, user.EmailConfirmed, user.IsEnabled, user.TwoFactorEnabled, user.RoleNames?.ToArray() ?? []);
}
public sealed record CrestUserWrite(string? UserName, string? Email, string? PhoneNumber, bool EmailConfirmed, bool IsEnabled, string[]? Roles, string? Password);
public sealed record CrestUserEnabled(bool Enabled);
