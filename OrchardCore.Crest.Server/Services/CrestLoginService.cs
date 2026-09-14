using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging;
using OrchardCore.Users;
using OrchardCore.Users.Events;
using OrchardCore.Users.Services;

namespace Crest.Services;

/// <summary>
/// The JSON login flow every Crest login surface shares (the admin login shell, and any
/// module surface with its own sign-in - a member portal, say). It fires the SAME
/// ILoginFormEvent sequence the stock MVC AccountController fires, in the same order,
/// so every policy hung on the standard seam (moderation, email confirmation,
/// audit-trail LoggedIn/LogInFailed recording, host-defined gates) applies to every
/// Crest login identically. The one adaptation: a veto handler returns an MVC
/// IActionResult (a page redirect) that a JSON endpoint cannot execute - it is
/// TRANSLATED instead (redirect target + the handler's TempData "error_*" messages
/// into the refusal payload) and any untranslatable result degrades to a generic
/// refusal. Refusal is always the fail-safe direction.
/// </summary>
public sealed class CrestLoginService(
    IUserService users,
    IEnumerable<ILoginFormEvent> loginEvents,
    ITempDataDictionaryFactory tempDataFactory,
    IUrlHelperFactory urlHelperFactory,
    ILogger<CrestLoginService> logger)
{
    /// <summary>Runs the credential + event sequence and, on success, issues the
    /// Identity application cookie. <paramref name="configureSession"/> lets the
    /// caller add AuthenticationProperties items (a surface's own session state)
    /// before the cookie is written.</summary>
    public async Task<CrestLoginResult> LoginAsync(
        ActionContext actionContext,
        string userName,
        string password,
        bool rememberMe,
        Action<AuthenticationProperties>? configureSession = null)
    {
        var httpContext = actionContext.HttpContext;
        var errors = new List<string>();
        void ReportError(string key, string message) => errors.Add(message);

        // 1. Pre-credential events (stock: before the password check).
        foreach (var loginEvent in loginEvents)
        {
            await loginEvent.LoggingInAsync(userName, ReportError);
        }

        if (errors.Count > 0)
        {
            await NotifyLoginFailedAsync(userName);
            return CrestLoginResult.Refused(new LoginRefusal(errors, null));
        }

        // 2. Credential + built-in policy checks (lockout, disabled, 2FA-required,
        // local-login-disabled) - unchanged behavior.
        var user = await users.AuthenticateAsync(userName, password, ReportError);
        if (user is null)
        {
            await NotifyLoginFailedAsync(userName);
            return CrestLoginResult.Refused(new LoginRefusal(errors, null));
        }

        // 3. Post-credential veto seam (stock: after CheckPasswordSignInAsync, before
        // the cookie is issued).
        foreach (var loginEvent in loginEvents)
        {
            var veto = await loginEvent.ValidatingLoginAsync(user);
            if (veto is not null)
            {
                var refusal = TranslateVeto(actionContext, veto, errors);
                await NotifyLoginFailedAsync(user);
                return CrestLoginResult.Refused(refusal);
            }
        }

        var principal = await users.CreatePrincipalAsync(user);
        var properties = new AuthenticationProperties { IsPersistent = rememberMe };
        configureSession?.Invoke(properties);
        await httpContext.SignInAsync(IdentityConstants.ApplicationScheme, principal, properties);

        // 4. Post-sign-in notifications (audit trail records LoggedIn here).
        foreach (var loginEvent in loginEvents)
        {
            await loginEvent.LoggedInAsync(user);
        }

        return CrestLoginResult.SignedIn(user, principal);
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
    private LoginRefusal TranslateVeto(ActionContext actionContext, IActionResult veto, List<string> errors)
    {
        var tempData = tempDataFactory.GetTempData(actionContext.HttpContext);
        foreach (var key in tempData.Keys.Where(key => key.StartsWith("error", StringComparison.OrdinalIgnoreCase)).ToArray())
        {
            if (tempData[key] is string message && !string.IsNullOrWhiteSpace(message))
            {
                errors.Add(message);
            }
        }

        var url = urlHelperFactory.GetUrlHelper(actionContext);
        var redirect = veto switch
        {
            RedirectResult redirectResult => redirectResult.Url,
            LocalRedirectResult localRedirect => localRedirect.Url,
            RedirectToActionResult toAction => url.Action(toAction.ActionName, toAction.ControllerName, toAction.RouteValues),
            RedirectToRouteResult toRoute => url.RouteUrl(toRoute.RouteName, toRoute.RouteValues),
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

/// <summary>Outcome of <see cref="CrestLoginService.LoginAsync"/>: either a signed-in
/// user + principal, or the refusal payload to return as 401.</summary>
public sealed record CrestLoginResult(IUser? User, ClaimsPrincipal? Principal, LoginRefusal? Refusal)
{
    public bool Succeeded => User is not null && Principal is not null;

    public static CrestLoginResult SignedIn(IUser user, ClaimsPrincipal principal) => new(user, principal, null);

    public static CrestLoginResult Refused(LoginRefusal refusal) => new(null, null, refusal);
}

/// <summary>The 401 payload for a refused login: the human-readable reasons, and the
/// page the equivalent MVC flow would have redirected to (null when none applies).</summary>
public sealed record LoginRefusal(IReadOnlyList<string> Errors, string? Redirect);
