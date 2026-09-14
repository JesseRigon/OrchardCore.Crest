using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Crest.Services;
using Crest.ViewModels;

namespace Crest.Controllers;

[ApiController]
[AutoValidateAntiforgeryToken]
[Route("api/crest/auth")]
public sealed class CrestAuthController(CrestLoginService loginService) : ControllerBase
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

    // The admin login shell's JSON adapter over the shared CrestLoginService flow (the
    // full stock ILoginFormEvent sequence, veto translated into the 401 payload).
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var result = await loginService.LoginAsync(ControllerContext, request.UserName, request.Password, request.RememberMe);
        if (!result.Succeeded)
        {
            return Unauthorized(result.Refusal);
        }

        return Ok(new AuthUser(
            true,
            result.Principal!.Identity?.Name ?? result.User!.UserName,
            result.Principal.FindAll(ClaimTypes.Role).Select(x => x.Value).ToArray()));
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
        return Ok(new AuthUser(false, null, []));
    }
}
