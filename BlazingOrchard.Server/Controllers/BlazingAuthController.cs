using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using OrchardCore.Users.Services;

namespace BlazingOrchard.Controllers;

[ApiController]
[IgnoreAntiforgeryToken]
[Route("api/blazing/auth")]
public sealed class BlazingAuthController(IUserService users) : ControllerBase
{
    [HttpGet("me")]
    public IActionResult Me()
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return Ok(new AuthUser(false, null, []));
        }

        return Ok(new AuthUser(
            true,
            User.Identity.Name,
            User.FindAll(ClaimTypes.Role).Select(x => x.Value).ToArray()));
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var errors = new List<string>();
        var user = await users.AuthenticateAsync(request.UserName, request.Password, (_, message) => errors.Add(message));

        if (user is null)
        {
            return Unauthorized(new { errors });
        }

        var principal = await users.CreatePrincipalAsync(user);
        await HttpContext.SignInAsync(IdentityConstants.ApplicationScheme, principal, new AuthenticationProperties
        {
            IsPersistent = request.RememberMe,
        });

        return Ok(new AuthUser(
            true,
            principal.Identity?.Name ?? user.UserName,
            principal.FindAll(ClaimTypes.Role).Select(x => x.Value).ToArray()));
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
        return Ok(new AuthUser(false, null, []));
    }
}

public sealed record LoginRequest(string UserName, string Password, bool RememberMe);
public sealed record AuthUser(bool IsAuthenticated, string? UserName, string[] Roles);
