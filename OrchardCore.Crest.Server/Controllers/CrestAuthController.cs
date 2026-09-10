using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging;
using OrchardCore.Users;
using OrchardCore.Users.Events;
using OrchardCore.Users.Services;
using Crest.ViewModels;

namespace Crest.Controllers;

[ApiController]
[AutoValidateAntiforgeryToken]
[Route("api/crest/auth")]
public sealed class CrestAuthController(
    IUserService users,
    IEnumerable<ILoginFormEvent> loginEvents,
    ITempDataDictionaryFactory tempDataFactory,
    ILogger<CrestAuthController> logger) : ControllerBase
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

    // The JSON login adapter fires the SAME ILoginFormEvent sequence the stock MVC
    // AccountController fires, in the same order, so every policy hung on the
    // standard seam (moderation, email confirmation, audit-trail LoggedIn/LogInFailed
    // recording, host-defined gates) applies to Crest logins identically. The one
    // adaptation: a veto handler returns an MVC IActionResult (a page redirect) that
    // a JSON endpoint cannot execute - it is TRANSLATED instead (redirect target +
    // the handler's TempData "error_*" messages into the 401 payload) and any
    // untranslatable result degrades to a generic refusal. Refusal is always the
    // fail-safe direction.
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var errors = new List<string>();
        void ReportError(string key, string message) => errors.Add(message);

        // 1. Pre-credential events (stock: before the password check).
        foreach (var loginEvent in loginEvents)
        {
            await loginEvent.LoggingInAsync(request.UserName, ReportError);
        }

        if (errors.Count > 0)
        {
            await NotifyLoginFailedAsync(request.UserName);
            return Unauthorized(new LoginRefusal(errors, null));
        }

        // 2. Credential + built-in policy checks (lockout, disabled, 2FA-required,
        // local-login-disabled) - unchanged behavior.
        var user = await users.AuthenticateAsync(request.UserName, request.Password, ReportError);
        if (user is null)
        {
            await NotifyLoginFailedAsync(request.UserName);
            return Unauthorized(new LoginRefusal(errors, null));
        }

        // 3. Post-credential veto seam (stock: after CheckPasswordSignInAsync, before
        // the cookie is issued).
        foreach (var loginEvent in loginEvents)
        {
            var veto = await loginEvent.ValidatingLoginAsync(user);
            if (veto is not null)
            {
                var refusal = TranslateVeto(veto, errors);
                await NotifyLoginFailedAsync(user);
                return Unauthorized(refusal);
            }
        }

        var principal = await users.CreatePrincipalAsync(user);
        await HttpContext.SignInAsync(IdentityConstants.ApplicationScheme, principal, new AuthenticationProperties
        {
            IsPersistent = request.RememberMe,
        });

        // 4. Post-sign-in notifications (audit trail records LoggedIn here).
        foreach (var loginEvent in loginEvents)
        {
            await loginEvent.LoggedInAsync(user);
        }

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

    private async Task NotifyLoginFailedAsync(string userName)
    {
        foreach (var loginEvent in loginEvents)
        {
            await loginEvent.LoggingInFailedAsync(userName);
        }
    }

    private async Task NotifyLoginFailedAsync(IUser user)
    {
        foreach (var loginEvent in loginEvents)
        {
            await loginEvent.LoggingInFailedAsync(user);
        }
    }

    /// <summary>Turns a veto handler's MVC result into JSON the SPA can act on:
    /// redirect-shaped results yield their target path, and the handler's TempData
    /// "error_*" messages (the stock MVC login convention) become the error list.</summary>
    private LoginRefusal TranslateVeto(IActionResult veto, List<string> errors)
    {
        var tempData = tempDataFactory.GetTempData(HttpContext);
        foreach (var key in tempData.Keys.Where(key => key.StartsWith("error", StringComparison.OrdinalIgnoreCase)).ToArray())
        {
            if (tempData[key] is string message && !string.IsNullOrWhiteSpace(message))
            {
                errors.Add(message);
            }
        }

        var redirect = veto switch
        {
            RedirectResult redirectResult => redirectResult.Url,
            LocalRedirectResult localRedirect => localRedirect.Url,
            RedirectToActionResult toAction => Url.Action(toAction.ActionName, toAction.ControllerName, toAction.RouteValues),
            RedirectToRouteResult toRoute => Url.RouteUrl(toRoute.RouteName, toRoute.RouteValues),
            _ => null,
        };

        // MVC's "~/path" app-base notation means nothing to the SPA client - resolve
        // it to the plain base-relative form.
        if (redirect is not null && redirect.StartsWith("~/", StringComparison.Ordinal))
        {
            redirect = redirect[1..];
        }

        if (redirect is null && veto is not RedirectResult and not LocalRedirectResult and not RedirectToActionResult and not RedirectToRouteResult)
        {
            logger.LogWarning("A login veto returned an untranslatable {ResultType}; refusing generically.", veto.GetType().Name);
        }

        if (errors.Count == 0)
        {
            errors.Add("Login is not allowed for this account.");
        }

        return new LoginRefusal(errors, redirect);
    }
}

/// <summary>The 401 payload for a refused login: the human-readable reasons, and the
/// page the equivalent MVC flow would have redirected to (null when none applies).</summary>
public sealed record LoginRefusal(IReadOnlyList<string> Errors, string? Redirect);
