using BlazingOrchard.Icons;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrchardCore.Settings;

namespace BlazingOrchard.Controllers;

[ApiController]
[IgnoreAntiforgeryToken]
[Route("api/blazing/icons/providers")]
public sealed class IconProvidersController(
    IAuthorizationService authorizationService,
    IIconProviderSettingsStore settingsStore,
    IIconifyLocalMirrorStore localMirrorStore) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<BlazingIconProvidersSettings>> GetAsync()
    {
        if (!await CanManageAsync())
        {
            return Forbid();
        }

        return Ok(Redact(await settingsStore.GetAsync(HttpContext.RequestAborted)));
    }

    [HttpPut]
    public async Task<ActionResult<BlazingIconProvidersSettings>> PutAsync(BlazingIconProvidersSettings settings)
    {
        if (!await CanManageAsync())
        {
            return Forbid();
        }

        if (!Uri.TryCreate(settings.Iconify.BaseUrl, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
        {
            ModelState.AddModelError(nameof(settings.Iconify.BaseUrl), "Iconify base URL must be an absolute http or https URL.");
            return ValidationProblem(ModelState);
        }

        var existing = await settingsStore.GetAsync(HttpContext.RequestAborted);
        var merged = settings with
        {
            Iconify = settings.Iconify with
            {
                ApiKey = string.IsNullOrWhiteSpace(settings.Iconify.ApiKey) ? existing.Iconify.ApiKey : settings.Iconify.ApiKey,
            },
        };

        return Ok(Redact(await settingsStore.SaveAsync(merged, HttpContext.RequestAborted)));
    }

    [HttpGet("iconify/local")]
    public async Task<ActionResult<IconifyLocalMirrorStatus>> GetIconifyLocalStatusAsync()
    {
        if (!await CanManageAsync())
        {
            return Forbid();
        }

        return Ok(await localMirrorStore.GetStatusAsync(HttpContext.RequestAborted));
    }

    private Task<bool> CanManageAsync() => authorizationService.AuthorizeAsync(User, SettingsPermissions.ManageSettings);

    private static BlazingIconProvidersSettings Redact(BlazingIconProvidersSettings settings) => settings with
    {
        Iconify = settings.Iconify with
        {
            ApiKey = null,
        },
    };
}
