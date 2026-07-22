using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;

namespace Crest.Controllers;

/// <summary>
/// Supplies Orchard's normal antiforgery request token to the same-origin WASM
/// client. The token remains bound to the browser's Orchard cookie and user.
/// </summary>
[ApiController]
[IgnoreAntiforgeryToken]
[Route("api/crest/antiforgery")]
public sealed class CrestAntiforgeryController(IAntiforgery antiforgery) : ControllerBase
{
    [HttpGet("token")]
    public ActionResult<CrestAntiforgeryToken> GetToken()
    {
        var tokens = antiforgery.GetAndStoreTokens(HttpContext);
        return Ok(new CrestAntiforgeryToken(tokens.HeaderName ?? "RequestVerificationToken", tokens.RequestToken ?? string.Empty));
    }
}

public sealed record CrestAntiforgeryToken(string HeaderName, string RequestToken);
