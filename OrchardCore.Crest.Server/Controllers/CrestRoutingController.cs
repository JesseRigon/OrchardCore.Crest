using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Crest;

namespace Crest.Controllers;

// Anonymous by design, same as CrestThemeController.Get: the WASM admin shell needs
// the tenant's real, configured AdminPath/LoginPath (see BlazorAdminThemeOptions,
// derived from AdminOptions.AdminUrlPrefix/UserOptions.LoginPath) before it knows
// whether the current visitor is authenticated - Login.razor and CrestAppContainer
// both build cross-shell navigation targets from this on every boot, logged in or
// not. Program.cs fetches it before WebAssemblyHostBuilder.Build() so those
// components always have a real, navigable path the instant they render.
[ApiController]
[Route("api/crest/routing")]
public sealed class CrestRoutingController(IOptions<BlazorAdminThemeOptions> options) : ControllerBase
{
    [HttpGet]
    public ActionResult<CrestRoutingResponse> Get()
        => Ok(new CrestRoutingResponse(options.Value.AdminPath, options.Value.LoginPath));
}

public sealed record CrestRoutingResponse(string AdminPath, string LoginPath);
